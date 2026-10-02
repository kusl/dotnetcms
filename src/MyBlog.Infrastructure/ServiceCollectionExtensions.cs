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

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString) || connectionString == "Data Source=myblog.db")
        {
            var dbPath = DatabasePathResolver.GetDatabasePath();
            connectionString = $"Data Source={dbPath}";
        }

        services.AddDbContext<BlogDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<ITelemetryLogRepository, TelemetryLogRepository>();

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISlugService, SlugService>();

        services.AddScoped<IMarkdownService, MarkdownService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddSingleton<IReaderTrackingService, ReaderTrackingService>();

        services.AddHttpClient<IImageDimensionService, ImageDimensionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MyBlog/1.0");
        });

        services.AddHostedService<TelemetryCleanupService>();
        services.AddHostedService<ImageCacheWarmerService>();

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
