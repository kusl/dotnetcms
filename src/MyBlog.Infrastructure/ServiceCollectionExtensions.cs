using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;

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
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISlugService, SlugService>();
        services.AddSingleton<IMarkdownService, MarkdownService>();
        services.AddScoped<IAuthService, AuthService>();

        // Background services
        services.AddHostedService<TelemetryCleanupService>();

        return services;
    }
}
