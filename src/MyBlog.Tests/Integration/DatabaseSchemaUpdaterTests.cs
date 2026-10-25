using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using Xunit;

namespace MyBlog.Tests.Integration;

public class DatabaseSchemaUpdaterTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;

    public DatabaseSchemaUpdaterTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task ApplyUpdatesAsync_OnFreshDatabase_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        var count = await _context.ImageDimensionCache.CountAsync(ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_WhenImageDimensionCacheTableMissing_CreatesIt()
    {
        var ct = TestContext.Current.CancellationToken;

        await _context.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS \"ImageDimensionCache\"", ct);

        var tableExists = await TableExistsAsync("ImageDimensionCache", ct);
        Assert.False(tableExists);

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        tableExists = await TableExistsAsync("ImageDimensionCache", ct);
        Assert.True(tableExists);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_CalledMultipleTimes_IsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        var count = await _context.ImageDimensionCache.CountAsync(ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_AfterRecreation_TableIsUsable()
    {
        var ct = TestContext.Current.CancellationToken;

        await _context.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS \"ImageDimensionCache\"", ct);

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        _context.ImageDimensionCache.Add(new ImageDimensionCache
        {
            Url = "https://example.com/test.png",
            Width = 800,
            Height = 600,
            LastCheckedUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        var cached = await _context.ImageDimensionCache
            .FirstOrDefaultAsync(x => x.Url == "https://example.com/test.png", ct);
        Assert.NotNull(cached);
        Assert.Equal(800, cached.Width);
        Assert.Equal(600, cached.Height);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_PreservesExistingData()
    {
        var ct = TestContext.Current.CancellationToken;

        _context.ImageDimensionCache.Add(new ImageDimensionCache
        {
            Url = "https://example.com/existing.png",
            Width = 1024,
            Height = 768,
            LastCheckedUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        var cached = await _context.ImageDimensionCache
            .FirstOrDefaultAsync(x => x.Url == "https://example.com/existing.png", ct);
        Assert.NotNull(cached);
        Assert.Equal(1024, cached.Width);
        Assert.Equal(768, cached.Height);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_DoesNotAffectOtherTables()
    {
        var ct = TestContext.Current.CancellationToken;

        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "schema-test-user",
            PasswordHash = "hash",
            Email = "schema@example.com",
            DisplayName = "Schema Test",
            CreatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == "schema-test-user", ct);
        Assert.NotNull(user);
        Assert.Equal("Schema Test", user.DisplayName);
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result) > 0;
    }
}
