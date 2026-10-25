using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Telemetry;

public static class TelemetryPathResolver
{
    public static string? GetLogsDirectory()
    {
        var preferredDir = Path.Combine(DatabasePathResolver.GetPreferredDataDirectory(), "telemetry", "logs");
        if (TryCreateAndVerify(preferredDir))
        {
            return preferredDir;
        }

        var localDir = Path.Combine(AppContext.BaseDirectory, "telemetry", "logs");
        if (TryCreateAndVerify(localDir))
        {
            return localDir;
        }

        return null;
    }

    private static bool TryCreateAndVerify(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var testFile = Path.Combine(path, $".write-test-{Guid.NewGuid()}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
