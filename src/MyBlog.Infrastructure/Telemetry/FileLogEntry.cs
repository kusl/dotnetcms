namespace MyBlog.Infrastructure.Telemetry;

internal sealed record FileLogEntry(
    string Timestamp,
    string Level,
    string? Category,
    string? Message,
    string TraceId,
    string SpanId,
    string? Exception);
