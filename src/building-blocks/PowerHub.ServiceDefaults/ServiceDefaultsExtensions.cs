using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PowerHub.ServiceDefaults;

/// <summary>
/// Cross-cutting technical mechanics every PowerHub service repeats: structured logs,
/// OpenTelemetry, Problem Details, and health endpoints. No business logic belongs here.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public const string ReadyTag = "ready";

    public static WebApplicationBuilder AddPowerHubServiceDefaults(
        this WebApplicationBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.FormatterName = JsonLogFormatter.FormatterName);
        builder.Logging.AddConsoleFormatter<JsonLogFormatter, JsonLogFormatterOptions>(options =>
        {
            options.Service = serviceName;
            options.Environment = builder.Environment.EnvironmentName;
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation();
                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                configureMetrics?.Invoke(metrics);
            });

        // Export only when a collector is configured so local runs and tests stay quiet.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.WithTracing(tracing => tracing.AddOtlpExporter())
                .WithMetrics(metrics => metrics.AddOtlpExporter());
        }

        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
            });

        builder.Services.AddHealthChecks();

        return builder;
    }

    /// <summary>
    /// Converts unhandled exceptions and bare error status codes into Problem Details
    /// without leaking exception content. Call first in the pipeline.
    /// </summary>
    public static WebApplication UsePowerHubErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    public static IEndpointRouteBuilder MapPowerHubHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Liveness never checks dependencies: a database outage must not restart healthy pods.
        endpoints.MapHealthChecks("/health/live", Minimal(_ => false));
        endpoints.MapHealthChecks("/health/ready", Minimal(check => check.Tags.Contains(ReadyTag)));
        endpoints.MapHealthChecks("/health/startup", Minimal(check => check.Tags.Contains(ReadyTag)));
        return endpoints;
    }

    private static HealthCheckOptions Minimal(Func<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration, bool> predicate) => new()
    {
        Predicate = predicate,
        // Status only: no check names, descriptions, or exception text.
        ResponseWriter = (context, report) => context.Response.WriteAsync(report.Status.ToString()),
    };
}
