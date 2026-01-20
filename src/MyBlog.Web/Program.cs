using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Constants;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Services;
using MyBlog.Infrastructure.Telemetry;
using MyBlog.Web.Components;
using MyBlog.Web.Hubs;
using MyBlog.Web.Middleware;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register SignalR
builder.Services.AddSignalR();

builder.Services.AddInfrastructure(builder.Configuration);

// Register TelemetryCleanupService as a hosted service
builder.Services.AddHostedService<TelemetryCleanupService>();

// Configure authentication
var sessionTimeout = builder.Configuration.GetValue("Authentication:SessionTimeoutMinutes", 30);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = AppConstants.AuthCookieName;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionTimeout);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Configuration.GetValue("Application:RequireHttps", false)
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Configure OpenTelemetry
var telemetryDir = TelemetryPathResolver.GetTelemetryDirectory();
var enableFileLogging = builder.Configuration.GetValue("Telemetry:EnableFileLogging", true);
var enableDbLogging = builder.Configuration.GetValue("Telemetry:EnableDatabaseLogging", true);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MyBlog"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
    });

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;

    if (enableDbLogging)
    {
        options.AddProcessor(new BatchLogRecordExportProcessor(
            new DatabaseLogExporter(builder.Services.BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>())));
    }

    if (enableFileLogging && telemetryDir is not null)
    {
        options.AddProcessor(new BatchLogRecordExportProcessor(
            new FileLogExporter(telemetryDir)));
    }
});

var app = builder.Build();

// Initialize database with proper migration support
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    // Ensure database exists and apply any pending model changes
    await context.Database.EnsureCreatedAsync();
    
    // Ensure ImageDimensionCache table exists (for existing databases)
    await EnsureImageDimensionCacheTableAsync(context, logger);

    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureAdminUserAsync();
}

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    if (builder.Configuration.GetValue("Application:RequireHttps", false))
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.UseLoginRateLimit();

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();

// Map the SignalR Hub
app.MapHub<ReaderHub>("/readerHub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Image endpoint
app.MapGet("/api/images/{id:guid}", async (Guid id, IImageRepository repo, CancellationToken ct) =>
{
    var image = await repo.GetByIdAsync(id, ct);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Data, image.ContentType, image.FileName);
});

app.Run();

/// <summary>
/// Ensures the ImageDimensionCache table exists for databases created before this feature was added.
/// This is a safe operation that only creates the table if it doesn't exist.
/// </summary>
static async Task EnsureImageDimensionCacheTableAsync(BlogDbContext context, ILogger logger)
{
    try
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // Check if table exists
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ImageDimensionCache'";
        var exists = Convert.ToInt64(await checkCommand.ExecuteScalarAsync()) > 0;

        if (!exists)
        {
            logger.LogInformation("Creating ImageDimensionCache table...");
            
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                CREATE TABLE ImageDimensionCache (
                    Url TEXT PRIMARY KEY NOT NULL,
                    Width INTEGER NOT NULL,
                    Height INTEGER NOT NULL,
                    LastCheckedUtc TEXT NOT NULL
                )";
            await createCommand.ExecuteNonQueryAsync();
            
            logger.LogInformation("ImageDimensionCache table created successfully.");
        }
    }
    catch (Exception ex)
    {
        // Log but don't fail startup - the app can work without this table
        logger.LogWarning(ex, "Could not ensure ImageDimensionCache table exists. Image dimension caching may not work.");
    }
}
