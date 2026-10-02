using System.Globalization;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PowerHub.Identity;
using PowerHub.Identity.Audit;
using PowerHub.Identity.Data;
using PowerHub.Identity.Email;
using PowerHub.Identity.Features;
using PowerHub.Identity.Tokens;
using PowerHub.ServiceDefaults;
using SessionOptions = PowerHub.Identity.SessionOptions;

var builder = WebApplication.CreateBuilder(args);

// The build-time OpenAPI generator starts the host without real configuration.
var generatingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

builder.AddPowerHubServiceDefaults(
    "identity-service",
    tracing => tracing.AddNpgsql(),
    metrics => metrics.AddMeter("Npgsql"));

AddValidatedOptions<JwtOptions>(JwtOptions.Section);
AddValidatedOptions<SessionOptions>(SessionOptions.Section);
AddValidatedOptions<EmailOptions>(EmailOptions.Section);
AddValidatedOptions<RateLimitOptions>(RateLimitOptions.Section);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<IdentityDb>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDb>(tags: [ServiceDefaultsExtensions.ReadyTag]);

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        // Length over composition rules, per current NIST guidance.
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = ""; // The user name is the email address.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<IdentityDb>();

builder.Services.AddSingleton<SigningKeys>();
builder.Services.AddScoped<AccessTokenIssuer>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AuditLog>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<EmailTemplates>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<EmailDispatcher>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<SigningKeys, IOptions<JwtOptions>>((options, keys, jwt) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKeys = keys.ValidationKeys,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Permissions.UsersRead, policy => policy.RequireClaim(AccessTokenIssuer.PermissionClaim, Permissions.UsersRead))
    .AddPolicy(Permissions.UsersManage, policy => policy.RequireClaim(AccessTokenIssuer.PermissionClaim, Permissions.UsersManage));

// Counters are per replica; a shared limit needs the ingress or a later ADR (Redis is deferred).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value.AuthPermitPerMinute,
            Window = TimeSpan.FromMinutes(1),
        }));
});

// Strict numbers keep the published contract free of "integer or string" unions.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);
builder.Services.AddValidation();
builder.Services.AddOpenApi();

var app = builder.Build();

if (args is ["migrate"])
{
    return await Cli.MigrateAsync(app.Services);
}

if (args is ["create-admin", var adminEmail])
{
    return await Cli.CreateAdminAsync(app.Services, adminEmail);
}

if (!generatingOpenApi)
{
    // Fail at startup, not on the first sign-in, when the signing key is missing or unreadable.
    app.Services.GetRequiredService<SigningKeys>();
}

app.UsePowerHubErrorHandling();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapPowerHubHealthEndpoints();
app.MapGet("/.well-known/jwks.json", (SigningKeys keys) => Results.Text(keys.JwksJson, "application/json"))
    .ExcludeFromDescription();
app.MapAuthEndpoints();
app.MapUserEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.RunAsync();
return 0;

void AddValidatedOptions<T>(string section)
    where T : class
{
    var options = builder.Services.AddOptions<T>().BindConfiguration(section).ValidateDataAnnotations();
    if (!generatingOpenApi)
    {
        options.ValidateOnStart();
    }
}

public partial class Program;
