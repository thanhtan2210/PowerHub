using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace PowerHub.ServiceDefaults;

public sealed class JsonLogFormatterOptions : ConsoleFormatterOptions
{
    public string Service { get; set; } = "unknown";

    public string Environment { get; set; } = "unknown";
}

/// <summary>
/// One JSON object per line on stdout with the fields NFR-OBS-002 requires:
/// service, environment, severity, timestamp, and trace identifiers.
/// </summary>
public sealed class JsonLogFormatter(IOptionsMonitor<JsonLogFormatterOptions> options)
    : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "powerhub-json";

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var current = options.CurrentValue;
        var buffer = new ArrayBufferWriter<byte>(512);

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("timestamp", DateTimeOffset.UtcNow);
            json.WriteString("level", logEntry.LogLevel.ToString());
            json.WriteString("service", current.Service);
            json.WriteString("environment", current.Environment);
            json.WriteString("category", logEntry.Category);
            json.WriteString("message", message);

            if (Activity.Current is { } activity)
            {
                json.WriteString("traceId", activity.TraceId.ToString());
                json.WriteString("spanId", activity.SpanId.ToString());
            }

            if (logEntry.State is IReadOnlyList<KeyValuePair<string, object?>> properties)
            {
                foreach (var (key, value) in properties)
                {
                    if (key != "{OriginalFormat}")
                    {
                        json.WriteString(key, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }

            if (logEntry.Exception is { } exception)
            {
                json.WriteString("exception", exception.ToString());
            }

            json.WriteEndObject();
        }

        textWriter.WriteLine(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }
}
