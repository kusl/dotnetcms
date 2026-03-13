using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using Xunit;

namespace MyBlog.Tests.Integration;

/// <summary>
/// Integration tests for the DatabaseSchemaUpdater.
/// Verifies that incremental schema updates work correctly for both
/// fresh databases and existing databases that are missing newer tables.
/// Uses in-memory SQLite for cross-platform compatibility.
/// </summary>
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

        // EnsureCreated already created all tables including ImageDimensionCache.
        // ApplyUpdatesAsync should be safe to call (idempotent).
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        // Verify the table is still usable
        var count = await _context.ImageDimensionCache.CountAsync(ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_WhenImageDimensionCacheTableMissing_CreatesIt()
    {
        var ct = TestContext.Current.CancellationToken;

        // Simulate an older database that was created before ImageDimensionCache existed
        await _context.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS \"ImageDimensionCache\"", ct);

        // Verify the table is actually gone
        var tableExists = await TableExistsAsync("ImageDimensionCache", ct);
        Assert.False(tableExists);

        // Apply incremental updates
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        // Verify the table was recreated
        tableExists = await TableExistsAsync("ImageDimensionCache", ct);
        Assert.True(tableExists);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_CalledMultipleTimes_IsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;

        // Call multiple times — should never throw
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        // Table should still be usable
        var count = await _context.ImageDimensionCache.CountAsync(ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_AfterRecreation_TableIsUsable()
    {
        var ct = TestContext.Current.CancellationToken;

        // Drop the table to simulate upgrade scenario
        await _context.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS \"ImageDimensionCache\"", ct);

        // Apply schema updates to recreate it
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        // Insert a record to prove the table schema is correct
        _context.ImageDimensionCache.Add(new ImageDimensionCache
        {
            Url = "https://example.com/test.png",
            Width = 800,
            Height = 600,
            LastCheckedUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        // Read it back
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

        // Insert data into the existing table
        _context.ImageDimensionCache.Add(new ImageDimensionCache
        {
            Url = "https://example.com/existing.png",
            Width = 1024,
            Height = 768,
            LastCheckedUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        // Run the schema updater (table already exists with data)
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        // Existing data should still be there
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

        // Insert a user to have data in another table
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

        // Run schema updates
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        // Other tables should be unaffected
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
