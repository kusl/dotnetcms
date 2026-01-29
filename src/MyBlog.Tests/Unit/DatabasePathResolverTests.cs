using MyBlog.Infrastructure.Data;
using Xunit;

namespace MyBlog.Tests.Unit;

/// <summary>
/// Unit tests for the DatabasePathResolver.
/// Tests XDG-compliant path resolution across platforms.
/// </summary>
public class DatabasePathResolverTests
{
    [Fact]
    public void GetDatabasePath_ReturnsNonEmptyPath()
    {
        var path = DatabasePathResolver.GetDatabasePath();

        Assert.False(string.IsNullOrEmpty(path));
    }

    [Fact]
    public void GetDatabasePath_EndsWithMyblogDb()
    {
        var path = DatabasePathResolver.GetDatabasePath();

        Assert.EndsWith("myblog.db", path);
    }

    [Fact]
    public void GetDatabasePath_ContainsMyBlogDirectory()
    {
        var path = DatabasePathResolver.GetDatabasePath();

        Assert.Contains("MyBlog", path);
    }

    [Fact]
    public void GetDataDirectory_ReturnsNonEmptyPath()
    {
        var path = DatabasePathResolver.GetDataDirectory();

        Assert.False(string.IsNullOrEmpty(path));
    }

    [Fact]
    public void GetDataDirectory_ContainsMyBlogDirectory()
    {
        var path = DatabasePathResolver.GetDataDirectory();

        Assert.EndsWith("MyBlog", path);
    }

    [Fact]
    public void GetDatabasePath_CreatesDirectoryIfNotExists()
    {
        var path = DatabasePathResolver.GetDatabasePath();
        var directory = Path.GetDirectoryName(path);

        Assert.NotNull(directory);
        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public void GetDatabasePath_ReturnsAbsolutePath()
    {
        var path = DatabasePathResolver.GetDatabasePath();

        Assert.True(Path.IsPathRooted(path));
    }

    [Fact]
    public void GetDataDirectory_ReturnsAbsolutePath()
    {
        var path = DatabasePathResolver.GetDataDirectory();

        Assert.True(Path.IsPathRooted(path));
    }

    [Fact]
    public void GetDatabasePath_ConsistentAcrossMultipleCalls()
    {
        var path1 = DatabasePathResolver.GetDatabasePath();
        var path2 = DatabasePathResolver.GetDatabasePath();

        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetDataDirectory_ConsistentAcrossMultipleCalls()
    {
        var path1 = DatabasePathResolver.GetDataDirectory();
        var path2 = DatabasePathResolver.GetDataDirectory();

        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetDatabasePath_IsInDataDirectory()
    {
        var dbPath = DatabasePathResolver.GetDatabasePath();
        var dataDir = DatabasePathResolver.GetDataDirectory();

        var dbDirectory = Path.GetDirectoryName(dbPath);
        Assert.Equal(dataDir, dbDirectory);
    }
}
