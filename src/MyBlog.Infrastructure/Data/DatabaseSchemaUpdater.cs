using Microsoft.EntityFrameworkCore;

namespace MyBlog.Infrastructure.Data;

/// <summary>
/// Handles incremental schema updates for existing databases.
/// Since we're not using formal EF Core migrations, this class ensures
/// new tables and columns are added to existing databases on deployment.
/// </summary>
public static class DatabaseSchemaUpdater
{
    /// <summary>
    /// Applies any pending schema updates to the database.
    /// This is safe to run multiple times - it only creates objects that don't exist.
    /// </summary>
    public static async Task ApplyUpdatesAsync(BlogDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // Check and create ImageDimensionCache table if it doesn't exist
        await EnsureImageDimensionCacheTableAsync(db);
    }

    private static async Task EnsureImageDimensionCacheTableAsync(BlogDbContext db)
    {
        var tableExists = await TableExistsAsync(db, "ImageDimensionCache");
        if (!tableExists)
        {
            // Create the ImageDimensionCache table using raw SQL
            // This matches the schema defined in BlogDbContext.OnModelCreating
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "ImageDimensionCache" (
                    "Url" TEXT NOT NULL CONSTRAINT "PK_ImageDimensionCache" PRIMARY KEY,
                    "Width" INTEGER NOT NULL,
                    "Height" INTEGER NOT NULL,
                    "LastCheckedUtc" TEXT NOT NULL
                )
                """);
        }
    }

    private static async Task<bool> TableExistsAsync(BlogDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }
}
