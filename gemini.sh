#!/bin/bash
set -euo pipefail

echo "Fixing Program.cs OpenTelemetry Exporter error..."

cat << 'EOF' > src/MyBlog.Web/Program.cs
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
using MyBlog.Web.Middleware;
using OpenTelemetry; // Required for BatchLogRecordExportProcessor
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionTimeout);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Configuration.GetValue("Application:RequireHttps", false)
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery();

// OpenTelemetry configuration
var serviceName = "MyBlog.Web";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(serviceName)
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Configure logging with OpenTelemetry
var telemetryDir = TelemetryPathResolver.GetTelemetryDirectory();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddConsoleExporter();

    // Add file exporter if we have a writable directory
    if (telemetryDir is not null)
    {
        var logsPath = Path.Combine(telemetryDir, "logs");
        Directory.CreateDirectory(logsPath);
        // FIX: Exporters must be wrapped in a Processor (Batch or Simple)
        logging.AddProcessor(new BatchLogRecordExportProcessor(new FileLogExporter(logsPath)));
    }
});

var app = builder.Build();

// Initialize database and seed admin user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await db.Database.MigrateAsync();

    // Seed admin user using the auth service
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureAdminUserAsync();
}

// Configure middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Rate limiting for login
app.UseLoginRateLimit();

// CRITICAL: Add logout endpoint BEFORE MapRazorComponents
// This handles the POST from MainLayout's logout form
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();

// Map Blazor components with interactive server rendering
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
EOF

echo "Done. You can now build the project."
