using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using PowerHub.Device.Data;
using PowerHub.Device.Features;
using PowerHub.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// The build-time OpenAPI generator starts the host without real configuration.
var generatingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

builder.AddPowerHubServiceDefaults(
    "device-service",
    tracing => tracing.AddNpgsql(),
    metrics => metrics.AddMeter("Npgsql").AddMeter(MqttAuthMetrics.MeterName));
builder.AddPowerHubJwtBearer(validateOnStart: !generatingOpenApi);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ChangeRecorder>();
builder.Services.AddSingleton<MqttAuthMetrics>();

// Integration events reach their consumers through the Outbox relay (ADR 0017), which
// authenticates to them with service tokens from Identity (ADR 0015).
builder.Services.AddPowerHubServiceTokens();
var outbox = builder.Services.AddOptions<OutboxOptions>()
    .BindConfiguration(OutboxOptions.Section)
    .ValidateDataAnnotations()
    .Validate(
        OutboxOptions.HasValidDestinations,
        "Each Outbox destination needs a name of at most 50 characters, an absolute BaseUrl, an Audience, and event types.")
    .Validate<IOptions<ServiceAuthOptions>>(
        (settings, auth) => settings.Destinations.Count == 0 || auth.Value.IsConfigured,
        "ServiceAuth must be configured when Outbox destinations are.");
if (!generatingOpenApi)
{
    outbox.ValidateOnStart();
}

builder.Services.AddHttpClient(OutboxRelay.HttpClientName).ConfigureHttpClient((services, client) =>
    client.Timeout = TimeSpan.FromSeconds(services.GetRequiredService<IOptions<OutboxOptions>>().Value.TimeoutSeconds));
builder.Services.AddSingleton<OutboxMetrics>();
builder.Services.AddSingleton<OutboxRelay>();
builder.Services.AddHostedService<OutboxRelayService>();

builder.Services.AddDbContext<DeviceDb>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("DeviceDb"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks().AddDbContextCheck<DeviceDb>(tags: [ServiceDefaultsExtensions.ReadyTag]);

// Strict numbers keep the published contract free of "integer or string" unions.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);
builder.Services.AddValidation();
builder.Services.AddOpenApi();

var app = builder.Build();

// Run as a release step or Kubernetes Job, never by serving replicas (INF-MIG-002).
if (args is ["migrate"])
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DeviceDb>().Database.MigrateAsync();
    return 0;
}

app.UsePowerHubErrorHandling();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();

app.MapPowerHubHealthEndpoints();
app.MapDeviceEndpoints();
app.MapMqttAuthEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.RunAsync();
return 0;

public partial class Program;
