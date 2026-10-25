namespace MyBlog.Core.Models;

public sealed class TelemetryLog
{
    public int Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public required string Level { get; set; }

    public required string Category { get; set; }

    public required string Message { get; set; }

    public string? Exception { get; set; }

    public string? TraceId { get; set; }

    public string? SpanId { get; set; }

    public string? Properties { get; set; }
}
