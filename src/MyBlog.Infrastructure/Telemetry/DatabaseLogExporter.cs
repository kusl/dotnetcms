using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyBlog.Infrastructure.Telemetry;

public sealed class DatabaseLogExporter : BaseExporter<LogRecord>, IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseLogExporter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

            foreach (var record in batch)
            {
                var log = new TelemetryLog
                {
                    TimestampUtc = record.Timestamp.ToUniversalTime(),
                    Level = record.LogLevel.ToString(),
                    Category = record.CategoryName ?? "Unknown",
                    Message = record.FormattedMessage ?? record.Body ?? "",
                    Exception = record.Exception?.ToString(),
                    TraceId = record.TraceId.ToString(),
                    SpanId = record.SpanId.ToString(),
                    Properties = SerializeAttributes(record.Attributes)
                };

                context.TelemetryLogs.Add(log);
            }

            context.SaveChanges();
            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    internal static string? SerializeAttributes(IReadOnlyList<KeyValuePair<string, object?>>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<string, object?>(attributes.Count);
        foreach (var attribute in attributes)
        {
            values[attribute.Key] = attribute.Value;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in values)
            {
                writer.WritePropertyName(key);
                WriteValue(writer, value);
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case short sh:
                writer.WriteNumberValue(sh);
                break;
            case byte by:
                writer.WriteNumberValue(by);
                break;
            case sbyte sb:
                writer.WriteNumberValue(sb);
                break;
            case ushort us:
                writer.WriteNumberValue(us);
                break;
            case uint ui:
                writer.WriteNumberValue(ui);
                break;
            case ulong ul:
                writer.WriteNumberValue(ul);
                break;
            case decimal m:
                writer.WriteNumberValue(m);
                break;
            case double d when double.IsFinite(d):
                writer.WriteNumberValue(d);
                break;
            case float f when float.IsFinite(f):
                writer.WriteNumberValue(f);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt);
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto);
                break;
            case Guid g:
                writer.WriteStringValue(g);
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
