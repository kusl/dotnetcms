namespace MyBlog.Infrastructure.Data;

/// <summary>
/// Resolves the database file path following XDG conventions.
/// </summary>
public static class DatabasePathResolver
{
    /// <summary>
    /// Gets the path for the SQLite database file.
    /// Priority: XDG_DATA_HOME > Platform-specific > Local fallback
    /// </summary>
    public static string GetDatabasePath()
    {
        var dataDir = GetDataDirectory();
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "myblog.db");
    }

    /// <summary>
    /// Gets the data directory following platform conventions.
    /// </summary>
    public static string GetDataDirectory()
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
            // Linux/Unix: Use XDG_DATA_HOME or fallback
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = !string.IsNullOrEmpty(xdgDataHome)
                ? xdgDataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        // If we can't write to the preferred location, use local directory
        var preferredDir = Path.Combine(baseDir, "MyBlog");
        try
        {
            Directory.CreateDirectory(preferredDir);
            var testFile = Path.Combine(preferredDir, ".write-test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return preferredDir;
        }
        catch
        {
            // Fallback to local directory
            return Path.Combine(AppContext.BaseDirectory, "data");
        }
    }
}
