namespace MyBlog.Infrastructure.Telemetry;

/// <summary>
/// Resolves the telemetry directory path following XDG conventions.
/// </summary>
public static class TelemetryPathResolver
{
    /// <summary>
    /// Attempts to get a writable telemetry directory.
    /// Returns null if no writable directory can be found.
    /// </summary>
    public static string? GetTelemetryDirectory()
    {
        // Try XDG/platform-specific location first
        var preferredDir = GetPreferredDirectory();
        if (TryCreateAndVerify(preferredDir))
        {
            return preferredDir;
        }

        // Fallback to local directory
        var localDir = Path.Combine(AppContext.BaseDirectory, "telemetry");
        if (TryCreateAndVerify(localDir))
        {
            return localDir;
        }

        // No writable directory available
        return null;
    }

    private static string GetPreferredDirectory()
    {
        string baseDir;

        if (OperatingSystem.IsWindows())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = !string.IsNullOrEmpty(xdgDataHome)
                ? xdgDataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(baseDir, "MyBlog", "telemetry");
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
