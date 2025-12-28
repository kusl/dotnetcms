using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyBlog.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry log exporter that writes to SQLite database.
/// </summary>
public sealed class DatabaseLogExporter : BaseExporter<LogRecord>
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of DatabaseLogExporter.</summary>
    public DatabaseLogExporter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
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
                    Properties = SerializeAttributes(record)
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

    private static string? SerializeAttributes(LogRecord record)
    {
        if (record.Attributes is null)
        {
            return null;
        }

        var dict = new Dictionary<string, object?>();
        foreach (var attr in record.Attributes)
        {
            dict[attr.Key] = attr.Value;
        }

        return dict.Count > 0 ? JsonSerializer.Serialize(dict) : null;
    }
}
