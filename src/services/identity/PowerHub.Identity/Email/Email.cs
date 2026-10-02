using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PowerHub.Identity.Email;

public sealed record EmailMessage(string To, string Subject, string Body);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Hands messages to a background sender so request latency does not reveal whether an
/// account exists (NFR-SEC-009). The queue is not durable: a lost message means the user
/// requests another link, and the proof itself lives in PostgreSQL.
/// </summary>
public sealed class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
}

public sealed class EmailDispatcher(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Never log the recipient or body: the body carries a one-time proof.
                logger.LogError(exception, "Email delivery failed for subject {Subject}", message.Subject);
            }
        }
    }
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var email = options.Value;
        if (string.IsNullOrWhiteSpace(email.Host))
        {
            logger.LogWarning("Email:Host is not configured; dropping message with subject {Subject}", message.Subject);
            return;
        }

        using var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(email.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            email.Host,
            email.Port,
            email.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrEmpty(email.Username))
        {
            await client.AuthenticateAsync(email.Username, email.Password ?? "", cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}

public sealed class EmailTemplates(IOptions<EmailOptions> options)
{
    private string BaseUrl => options.Value.FrontendBaseUrl.TrimEnd('/');

    public EmailMessage ConfirmEmail(string to, string token) => new(
        to,
        "Confirm your PowerHub email address",
        $"Welcome to PowerHub.\n\nConfirm your email address to activate your account:\n{BaseUrl}/confirm-email?token={token}\n\nIf you did not create this account, ignore this message.");

    public EmailMessage AlreadyRegistered(string to) => new(
        to,
        "PowerHub registration attempt",
        $"Someone tried to register a PowerHub account with this email address, but an account already exists.\n\nSign in: {BaseUrl}/sign-in\nForgot your password: {BaseUrl}/forgot-password\n\nIf this was not you, no action is needed.");

    public EmailMessage ResetPassword(string to, string token) => new(
        to,
        "Reset your PowerHub password",
        $"Use this link to choose a new password. It can be used once and expires soon:\n{BaseUrl}/reset-password?token={token}\n\nIf you did not request this, ignore this message.");
}
