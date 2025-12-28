analyze this 
  MyBlog.Core net10.0 succeeded (0.1s) → MyBlog.Core/bin/Debug/net10.0/MyBlog.Core.dll
  MyBlog.Infrastructure net10.0 failed with 1 error(s) (0.1s)
    /home/kushal/src/dotnet/MyBlog/src/MyBlog.Infrastructure/Telemetry/DatabaseLogExporter.cs(35,53): error CS1061: 'DateTime' does not contain a definition for 'UtcDateTime' and no accessible extension method 'UtcDateTime' accepting a first argument of type 'DateTime' could be found (are you missing a using directive or an assembly reference?)
Build failed with 1 error(s) in 0.9s
```csharp /home/kushal/src/dotnet/MyBlog/src/MyBlog.Infrastructure/Telemetry/DatabaseLogExporter.cs
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
                    TimestampUtc = record.Timestamp.UtcDateTime,
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
```

Based on the build output and the code provided, here is the analysis and the solution.

### The Problem

The error `CS1061: 'DateTime' does not contain a definition for 'UtcDateTime'` occurs because **`LogRecord.Timestamp` is of type `DateTime**`, but you are trying to access a property named `.UtcDateTime` on it.

The property `.UtcDateTime` exists on the `DateTimeOffset` struct, but **not** on the standard `DateTime` struct.

### The Fix

Since `record.Timestamp` is already a `DateTime` (and OpenTelemetry typically sets the `Kind` to Utc automatically), you simply need to remove `.UtcDateTime`.

Change **Line 35** in `DatabaseLogExporter.cs` from this:

```csharp
TimestampUtc = record.Timestamp.UtcDateTime,

```

To this:

```csharp
TimestampUtc = record.Timestamp, 

```

---

### Corrected Code Block

Here is the corrected `Export` method:

```csharp
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
                // FIX: Removed .UtcDateTime
                TimestampUtc = record.Timestamp, 
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

```

### Technical Note

If you want to be absolutely certain the date is stored as UTC (even if the incoming record has a `Local` or `Unspecified` kind), you can use the method `.ToUniversalTime()` instead of the property:

```csharp
TimestampUtc = record.Timestamp.ToUniversalTime(),

```

However, simply assigning `record.Timestamp` is usually sufficient in the OpenTelemetry context.

**Would you like me to analyze the `TelemetryLog` entity class to ensure the database schema matches these fields?**









