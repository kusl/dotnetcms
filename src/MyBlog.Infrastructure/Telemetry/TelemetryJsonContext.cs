using System.Text.Json.Serialization;

namespace MyBlog.Infrastructure.Telemetry;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(FileLogEntry))]
internal partial class TelemetryJsonContext : JsonSerializerContext
{
}
