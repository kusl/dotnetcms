namespace MyBlog.Core.Models;

/// <summary>
/// Represents a telemetry log entry stored in the database.
/// </summary>
public sealed class TelemetryLog
{
    /// <summary>Gets or sets the auto-increment primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the log entry.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>Gets or sets the log level (Information, Warning, Error, etc.).</summary>
    public required string Level { get; set; }

    /// <summary>Gets or sets the category/source of the log.</summary>
    public required string Category { get; set; }

    /// <summary>Gets or sets the log message.</summary>
    public required string Message { get; set; }

    /// <summary>Gets or sets the exception details if any.</summary>
    public string? Exception { get; set; }

    /// <summary>Gets or sets the distributed trace ID.</summary>
    public string? TraceId { get; set; }

    /// <summary>Gets or sets the span ID within the trace.</summary>
    public string? SpanId { get; set; }

    /// <summary>Gets or sets additional properties as JSON.</summary>
    public string? Properties { get; set; }
}
