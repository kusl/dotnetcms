namespace MyBlog.Infrastructure.Telemetry;

internal static class LogFileRetention
{
    internal const string FilePattern = "logs_*.json";

    internal static (int Deleted, int Failed) Apply(string directory, DateTime cutoffUtc, long maxTotalBytes)
    {
        if (!Directory.Exists(directory))
        {
            return (0, 0);
        }

        var files = new DirectoryInfo(directory)
            .GetFiles(FilePattern)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        var deleted = 0;
        var failed = 0;
        long keptBytes = 0;

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var expired = file.LastWriteTimeUtc < cutoffUtc;
            var overBudget = i > 0 && keptBytes + file.Length > maxTotalBytes;

            if (!expired && !overBudget)
            {
                keptBytes += file.Length;
                continue;
            }

            try
            {
                file.Delete();
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        return (deleted, failed);
    }
}
