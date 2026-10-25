namespace MyBlog.Infrastructure.Data;

public static class DatabasePathResolver
{
    public static string GetDatabasePath()
    {
        var dataDir = GetDataDirectory();
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "myblog.db");
    }

    public static string GetDataDirectory()
    {
        var preferredDir = GetPreferredDataDirectory();
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
            return Path.Combine(AppContext.BaseDirectory, "data");
        }
    }

    internal static string GetPreferredDataDirectory()
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

        return Path.Combine(baseDir, "MyBlog");
    }
}
