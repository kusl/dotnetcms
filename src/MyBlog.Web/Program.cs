using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Constants;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Telemetry;
using MyBlog.Web.Components;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);

// Configure authentication
var sessionTimeout = builder.Configuration.GetValue("Authentication:SessionTimeoutMinutes", 30);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = AppConstants.AuthCookieName;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
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
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
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

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await context.Database.EnsureCreatedAsync();

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
