using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerHub.Device.Data;
using PowerHub.ServiceDefaults;

namespace PowerHub.Device.Features;

public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    /// <summary>Turned off only by tests that drive the relay themselves.</summary>
    public bool RelayEnabled { get; set; } = true;

    [Range(1, 60)]
    public int PollSeconds { get; set; } = 2;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 50;

    [Range(10, 600)]
    public int LeaseSeconds { get; set; } = 60;

    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 20;

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Receiving services by name; the name is stored with each delivery (ADR 0017).</summary>
    public IDictionary<string, OutboxDestination> Destinations { get; } = new Dictionary<string, OutboxDestination>(StringComparer.Ordinal);

    public static bool HasValidDestinations(OutboxOptions options) =>
        options.Destinations.All(pair =>
            pair.Key.Length is > 0 and <= 50
            && Uri.TryCreate(pair.Value.BaseUrl, UriKind.Absolute, out _)
            && !string.IsNullOrEmpty(pair.Value.Audience)
            && pair.Value.EventTypes.Count > 0);
}

public sealed class OutboxDestination
{
    /// <summary>Internal base address of the receiver; events are posted to <c>internal/v1/events</c> under it.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Service token audience of the receiver (ADR 0015).</summary>
    public string Audience { get; set; } = "";

    public IList<string> EventTypes { get; } = [];
}

public sealed record DestinationBacklog(string Destination, long Pending, long DeadLettered, double OldestPendingSeconds);

/// <summary>Outbox age, backlog, attempts, failures, and dead letters (INT-006).</summary>
public sealed class OutboxMetrics : IDisposable
{
    private readonly Meter _meter = new(MqttAuthMetrics.MeterName);
    private readonly Counter<long> _delivered;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _deadLettered;
    private IReadOnlyList<DestinationBacklog> _backlog = [];

    public OutboxMetrics()
    {
        _delivered = _meter.CreateCounter<long>("powerhub.outbox.delivered", description: "Events delivered to a destination.");
        _failed = _meter.CreateCounter<long>("powerhub.outbox.failed", description: "Delivery attempts that failed.");
        _deadLettered = _meter.CreateCounter<long>("powerhub.outbox.dead_lettered", description: "Deliveries moved to the dead-letter state.");
        _meter.CreateObservableGauge(
            "powerhub.outbox.pending",
            () => _backlog.Select(b => new Measurement<long>(b.Pending, Tag(b.Destination))),
            description: "Deliveries not yet made.");
        _meter.CreateObservableGauge(
            "powerhub.outbox.dead_letters",
            () => _backlog.Select(b => new Measurement<long>(b.DeadLettered, Tag(b.Destination))),
            description: "Deliveries waiting for an operator.");
        _meter.CreateObservableGauge(
            "powerhub.outbox.oldest_pending_age",
            () => _backlog.Select(b => new Measurement<double>(b.OldestPendingSeconds, Tag(b.Destination))),
            unit: "s",
            description: "Age of the oldest event not yet delivered.");
    }

    public void Delivered(string destination) => _delivered.Add(1, Tag(destination));

    public void Failed(string destination) => _failed.Add(1, Tag(destination));

    public void DeadLettered(string destination) => _deadLettered.Add(1, Tag(destination));

    public void Report(IReadOnlyList<DestinationBacklog> backlog) => _backlog = backlog;

    public void Dispose() => _meter.Dispose();

    private static KeyValuePair<string, object?> Tag(string destination) => new("destination", destination);
}

/// <summary>
/// Delivers Outbox events to the configured destinations at least once (ADR 0003, ADR 0017).
/// Replicas share the work: each claims due deliveries under a lease, so a crashed replica's
/// work is picked up again when its lease expires, and receivers deduplicate on event id.
/// </summary>
public sealed class OutboxRelay(
    IServiceScopeFactory scopes,
    IHttpClientFactory httpClients,
    ServiceTokenClient tokens,
    IOptions<OutboxOptions> options,
    OutboxMetrics metrics,
    TimeProvider time,
    ILogger<OutboxRelay> logger)
{
    public const string HttpClientName = "powerhub-outbox";
    public const string EventsPath = "internal/v1/events";

    private const int FanOutBatch = 500;
    private const int MaxErrorLength = 500;
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(10);

    /// <summary>
    /// One pass: records deliveries for new events, claims and delivers the due ones, and
    /// reports the backlog. Returns how many deliveries were attempted.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.Destinations.Count == 0)
        {
            return 0;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceDb>();

        await FanOutAsync(db, settings, cancellationToken);
        var (claimed, lease) = await ClaimAsync(db, settings, cancellationToken);

        var messageIds = claimed.Select(delivery => delivery.MessageId).Distinct().ToList();
        var payloads = await db.Outbox
            .Where(message => messageIds.Contains(message.Id))
            .ToDictionaryAsync(message => message.Id, message => message.Payload, cancellationToken);

        foreach (var delivery in claimed)
        {
            var outcome = await DeliverAsync(settings.Destinations[delivery.Destination], payloads[delivery.MessageId], cancellationToken);
            await RecordAsync(db, delivery, lease, outcome, settings, cancellationToken);
        }

        await ReportBacklogAsync(db, settings, cancellationToken);
        return claimed.Count;
    }

    /// <summary>Exponential from one second, capped at ten minutes, with ±20 % jitter.</summary>
    public static TimeSpan Backoff(int attempts)
    {
        var exponential = FirstRetry * Math.Pow(2, Math.Min(attempts - 1, 20));
        var capped = exponential < LongestRetry ? exponential : LongestRetry;
        return capped * (RandomNumberGenerator.GetInt32(80, 121) / 100.0);
    }

    /// <summary>Gives every subscribed destination a delivery row for each event it has not seen, oldest first.</summary>
    private async Task FanOutAsync(DeviceDb db, OutboxOptions settings, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        foreach (var (name, destination) in settings.Destinations)
        {
            var eventTypes = destination.EventTypes.ToArray();
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO outbox_deliveries (destination, message_id, attempts, next_attempt_at)
                SELECT {name}, m.id, 0, {now}
                FROM outbox_messages m
                WHERE m.event_type = ANY({eventTypes})
                  AND NOT EXISTS (
                    SELECT 1 FROM outbox_deliveries d WHERE d.destination = {name} AND d.message_id = m.id)
                ORDER BY m.id
                LIMIT {FanOutBatch}
                ON CONFLICT DO NOTHING
                """,
                cancellationToken);
        }
    }

    /// <summary>
    /// Locks due rows that no other replica holds and stamps them with this pass's lease in
    /// one short transaction; delivery happens after it commits (DATA-CON-002).
    /// </summary>
    private async Task<(List<OutboxDelivery> Claimed, DateTimeOffset Lease)> ClaimAsync(
        DeviceDb db, OutboxOptions settings, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        // Whole milliseconds survive the round trip to PostgreSQL exactly, so the lease can fence updates.
        var leaseTicks = (now + TimeSpan.FromSeconds(settings.LeaseSeconds)).UtcTicks;
        var lease = new DateTimeOffset(leaseTicks - (leaseTicks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
        var names = settings.Destinations.Keys.ToArray();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await db.Deliveries
            .FromSql(
                $"""
                SELECT * FROM outbox_deliveries
                WHERE destination = ANY({names})
                  AND delivered_at IS NULL AND dead_lettered_at IS NULL
                  AND next_attempt_at <= {now}
                  AND (locked_until IS NULL OR locked_until <= {now})
                ORDER BY next_attempt_at
                LIMIT {settings.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var delivery in claimed)
        {
            delivery.LockedUntil = lease;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return (claimed, lease);
    }

    private async Task<Outcome> DeliverAsync(OutboxDestination destination, string payload, CancellationToken cancellationToken)
    {
        try
        {
            var token = await tokens.GetTokenAsync(destination.Audience, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{destination.BaseUrl.TrimEnd('/')}/{EventsPath}"))
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Outcome.Success;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                tokens.Invalidate(destination.Audience);
            }

            // A client error other than these cannot succeed by sending the same request again.
            var status = (int)response.StatusCode;
            var permanent = status is >= 400 and < 500 and not (401 or 408 or 429);
            return new Outcome(false, permanent, $"HTTP {status}");
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new Outcome(false, false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>Updates the row only while this pass's lease is still on it.</summary>
    private async Task RecordAsync(
        DeviceDb db, OutboxDelivery delivery, DateTimeOffset lease, Outcome outcome, OutboxOptions settings, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var held = db.Deliveries.Where(row =>
            row.Destination == delivery.Destination && row.MessageId == delivery.MessageId && row.LockedUntil == lease);

        if (outcome.Delivered)
        {
            await held.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.DeliveredAt, (DateTimeOffset?)now)
                    .SetProperty(row => row.LockedUntil, (DateTimeOffset?)null)
                    .SetProperty(row => row.LastError, (string?)null),
                cancellationToken);
            metrics.Delivered(delivery.Destination);
            return;
        }

        var attempts = delivery.Attempts + 1;
        var deadLetter = outcome.Permanent || attempts >= settings.MaxAttempts;
        DateTimeOffset? deadLetteredAt = deadLetter ? now : null;
        var nextAttemptAt = now + Backoff(attempts);
        var error = outcome.Error.Length > MaxErrorLength ? outcome.Error[..MaxErrorLength] : outcome.Error;

        await held.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.Attempts, attempts)
                .SetProperty(row => row.NextAttemptAt, nextAttemptAt)
                .SetProperty(row => row.DeadLetteredAt, deadLetteredAt)
                .SetProperty(row => row.LastError, error)
                .SetProperty(row => row.LockedUntil, (DateTimeOffset?)null),
            cancellationToken);
        metrics.Failed(delivery.Destination);

        if (deadLetter)
        {
            metrics.DeadLettered(delivery.Destination);
            logger.LogWarning(
                "Delivery of event {EventId} to {Destination} dead-lettered after {Attempts} attempts: {Error}",
                delivery.MessageId,
                delivery.Destination,
                attempts,
                error);
        }
    }

    private async Task ReportBacklogAsync(DeviceDb db, OutboxOptions settings, CancellationToken cancellationToken)
    {
        var pending = await (
                from delivery in db.Deliveries
                where delivery.DeliveredAt == null && delivery.DeadLetteredAt == null
                join message in db.Outbox on delivery.MessageId equals message.Id
                group message.OccurredAt by delivery.Destination into byDestination
                select new { Destination = byDestination.Key, Count = byDestination.LongCount(), Oldest = byDestination.Min() })
            .ToListAsync(cancellationToken);

        var deadLetters = await db.Deliveries
            .Where(delivery => delivery.DeadLetteredAt != null)
            .GroupBy(delivery => delivery.Destination)
            .Select(byDestination => new { Destination = byDestination.Key, Count = byDestination.LongCount() })
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        metrics.Report([.. settings.Destinations.Keys.Select(name =>
        {
            var waiting = pending.FirstOrDefault(row => row.Destination == name);
            return new DestinationBacklog(
                name,
                waiting?.Count ?? 0,
                deadLetters.FirstOrDefault(row => row.Destination == name)?.Count ?? 0,
                waiting is null ? 0 : Math.Max(0, (now - waiting.Oldest).TotalSeconds));
        })]);
    }

    private sealed record Outcome(bool Delivered, bool Permanent, string Error)
    {
        public static readonly Outcome Success = new(true, false, "");
    }
}

/// <summary>Runs the relay on a fixed interval, and back to back while full batches keep coming.</summary>
public sealed class OutboxRelayService(OutboxRelay relay, IOptions<OutboxOptions> options, ILogger<OutboxRelayService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.RelayEnabled || settings.Destinations.Count == 0)
        {
            logger.LogInformation("Outbox relay is idle: no destinations are configured");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollSeconds));
        do
        {
            try
            {
                int attempted;
                do
                {
                    attempted = await relay.RunOnceAsync(stoppingToken);
                }
                while (attempted == settings.BatchSize);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox relay pass failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
