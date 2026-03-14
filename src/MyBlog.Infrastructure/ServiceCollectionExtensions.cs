using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Core.Services;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;
using MyBlog.Infrastructure.Telemetry;

namespace MyBlog.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds infrastructure services to the DI container.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString) || connectionString == "Data Source=myblog.db")
        {
            // Use XDG-compliant path
            var dbPath = DatabasePathResolver.GetDatabasePath();
            connectionString = $"Data Source={dbPath}";
        }

        services.AddDbContext<BlogDbContext>(options =>
            options.UseSqlite(connectionString));

        // Repositories
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<ITelemetryLogRepository, TelemetryLogRepository>();

        // Services
        // Register the password hasher so PasswordService gets it via DI,
        // and hashing options (like iteration count) can be configured globally.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISlugService, SlugService>();

        // MarkdownService is Scoped because it depends on Scoped IImageDimensionService
        services.AddScoped<IMarkdownService, MarkdownService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddSingleton<IReaderTrackingService, ReaderTrackingService>();

        // Image Dimension Service (With HttpClient for fetching image headers)
        services.AddHttpClient<IImageDimensionService, ImageDimensionService>();

        // Background services
        services.AddHostedService<TelemetryCleanupService>();
        // Cache Warmer - runs on startup to pre-fetch dimensions for existing images
        services.AddHostedService<ImageCacheWarmerService>();

        // Telemetry log exporters
        // FileLogExporter requires a directory string in its constructor —
        // use a factory so DI can construct it with the resolved path.
        var enableFileLogging = configuration.GetValue("Telemetry:EnableFileLogging", true);
        if (enableFileLogging)
        {
            var telemetryDir = TelemetryPathResolver.GetTelemetryDirectory();
            if (telemetryDir is not null)
            {
                var logsDir = Path.Combine(telemetryDir, "logs");
                services.AddSingleton<FileLogExporter>(sp => new FileLogExporter(logsDir));
                services.AddHostedService(sp => sp.GetRequiredService<FileLogExporter>());
            }
        }

        var enableDbLogging = configuration.GetValue("Telemetry:EnableDatabaseLogging", true);
        if (enableDbLogging)
        {
            services.AddSingleton<DatabaseLogExporter>();
            services.AddHostedService(sp => sp.GetRequiredService<DatabaseLogExporter>());
        }

        return services;
    }
}
