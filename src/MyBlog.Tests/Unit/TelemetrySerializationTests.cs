using System.Text.Json;
using MyBlog.Infrastructure.Telemetry;
using Xunit;

namespace MyBlog.Tests.Unit;

public class TelemetrySerializationTests
{
    [Fact]
    public void FileLogEntry_SourceGeneratedJson_MatchesReflectionOutput()
    {
        var entry = new FileLogEntry(
            "2026-10-02T12:00:00.0000000+00:00",
            "Information",
            "MyBlog.Category",
            "Message with <html> & \"quotes\" 👋",
            "0123456789abcdef0123456789abcdef",
            "0123456789abcdef",
            null);

        var expected = JsonSerializer.Serialize(
            new
            {
                entry.Timestamp,
                entry.Level,
                entry.Category,
                entry.Message,
                entry.TraceId,
                entry.SpanId,
                entry.Exception
            },
            new JsonSerializerOptions { WriteIndented = true });

        var actual = JsonSerializer.Serialize(entry, TelemetryJsonContext.Default.FileLogEntry);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SerializeAttributes_WithNull_ReturnsNull() =>
        Assert.Null(DatabaseLogExporter.SerializeAttributes(null));

    [Fact]
    public void SerializeAttributes_WithEmpty_ReturnsNull() =>
        Assert.Null(DatabaseLogExporter.SerializeAttributes([]));

    [Fact]
    public void SerializeAttributes_WithPrimitives_MatchesReflectionOutput()
    {
        var timestamp = new DateTime(2026, 10, 2, 12, 30, 45, DateTimeKind.Utc);
        var id = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        List<KeyValuePair<string, object?>> attributes =
        [
            new("text", "value <b>"),
            new("int", 42),
            new("long", 9_000_000_000L),
            new("bool", true),
            new("double", 1.5),
            new("none", null),
            new("when", timestamp),
            new("id", id),
            new("{OriginalFormat}", "Value {Int}")
        ];
        var dictionary = new Dictionary<string, object?>();
        foreach (var attribute in attributes)
        {
            dictionary[attribute.Key] = attribute.Value;
        }

        var expected = JsonSerializer.Serialize(dictionary);
        var actual = DatabaseLogExporter.SerializeAttributes(attributes);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SerializeAttributes_WithDuplicateKeys_LastValueWins()
    {
        List<KeyValuePair<string, object?>> attributes =
        [
            new("key", "first"),
            new("key", "second")
        ];

        Assert.Equal("{\"key\":\"second\"}", DatabaseLogExporter.SerializeAttributes(attributes));
    }

    [Fact]
    public void SerializeAttributes_WithComplexValue_WritesInvariantString()
    {
        List<KeyValuePair<string, object?>> attributes =
        [
            new("version", new Version(1, 2, 3))
        ];

        Assert.Equal("{\"version\":\"1.2.3\"}", DatabaseLogExporter.SerializeAttributes(attributes));
    }
}
