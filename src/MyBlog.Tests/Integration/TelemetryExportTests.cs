using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class TelemetryExportTests
{
    [Fact]
    public async Task AddLocalExporters_WithDatabaseLogging_PersistsLogRecords()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:EnableFileLogging"] = "false",
                ["Telemetry:EnableDatabaseLogging"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDbContext<BlogDbContext>(options => options.UseSqlite(connection));
        services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Warning)
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.AddLocalExporters(configuration);
            }));

        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BlogDbContext>().Database.EnsureCreatedAsync(ct);
        }

        provider.GetRequiredService<ILogger<TelemetryExportTests>>().LogWarning("Exported {Value}", 42);
        Assert.True(provider.GetRequiredService<LoggerProvider>().ForceFlush(10_000));

        using var verifyScope = provider.CreateScope();
        var log = await verifyScope.ServiceProvider.GetRequiredService<BlogDbContext>()
            .TelemetryLogs.AsNoTracking()
            .Where(l => l.Category == typeof(TelemetryExportTests).FullName)
            .SingleAsync(ct);
        Assert.Equal("Warning", log.Level);
        Assert.Equal("Exported 42", log.Message);
    }

    [Fact]
    public void FileLogExporter_OnShutdown_WritesValidJsonArray()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"myblog-file-logs-{Guid.NewGuid()}");
        try
        {
            using (var factory = LoggerFactory.Create(logging => logging
                .SetMinimumLevel(LogLevel.Information)
                .AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.AddProcessor(new BatchLogRecordExportProcessor(new FileLogExporter(directory)));
                })))
            {
                factory.CreateLogger("FileTest").LogInformation("First {Number}", 1);
                factory.CreateLogger("FileTest").LogInformation("Second {Number}", 2);
            }

            var file = Assert.Single(Directory.GetFiles(directory, "logs_*.json"));
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var messages = document.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("Message").GetString())
                .ToList();
            Assert.Equal(new string?[] { "First 1", "Second 2" }, messages);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
