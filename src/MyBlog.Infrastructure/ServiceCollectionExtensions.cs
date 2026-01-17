using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;

namespace MyBlog.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString) || connectionString == "Data Source=myblog.db")
        {
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
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISlugService, SlugService>();
        // REPLACED: Scoped because it now depends on Scoped/Transient DB access via IImageDimensionService logic
        services.AddScoped<IMarkdownService, MarkdownService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddSingleton<IReaderTrackingService, ReaderTrackingService>();

        // NEW: Image Dimension Service (With HttpClient)
        services.AddHttpClient<IImageDimensionService, ImageDimensionService>();

        // Background services
        services.AddHostedService<TelemetryCleanupService>();
        // NEW: Cache Warmer
        services.AddHostedService<ImageCacheWarmerService>();

        return services;
    }
}
