#!/bin/bash
set -euo pipefail

echo "=============================================="
echo "  Fixing Access Denied / 404 Issue"
echo "=============================================="

# 1. Update Login.razor to include the Admin Role Claim
# ----------------------------------------------------
# We are adding 'new(ClaimTypes.Role, AppConstants.AdminRole)' to the claims list.
echo "Updating Login.razor..."
cat << 'EOF' > src/MyBlog.Web/Components/Pages/Login.razor
@page "/login"
@inject IAuthService AuthService
@inject NavigationManager Navigation
@inject IHttpContextAccessor HttpContextAccessor
@using System.Security.Claims
@using Microsoft.AspNetCore.Authentication
@using Microsoft.AspNetCore.Authentication.Cookies

<PageTitle>Login</PageTitle>

<div class="login-page">
    <h1>Login</h1>

    @if (!string.IsNullOrEmpty(_error))
    {
        <div class="error-message">@_error</div>
    }

    <form method="post" @onsubmit="HandleLogin" @formname="login">
        <AntiforgeryToken />
        
        <div class="form-group">
            <label for="username">Username</label>
            <input type="text" id="username" name="username" @bind="_username" required />
        </div>

        <div class="form-group">
            <label for="password">Password</label>
            <input type="password" id="password" name="password" @bind="_password" required />
        </div>

        <button type="submit" class="btn btn-primary">Login</button>
    </form>
</div>

@code {
    private string _username = "";
    private string _password = "";
    private string? _error;

    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromForm(Name = "username")]
    public string? FormUsername { get; set; }

    [SupplyParameterFromForm(Name = "password")]
    public string? FormPassword { get; set; }

    private async Task HandleLogin()
    {
        var username = FormUsername ?? _username;
        var password = FormPassword ?? _password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _error = "Username and password are required";
            return;
        }

        var user = await AuthService.AuthenticateAsync(username, password);
        if (user is null)
        {
            _error = "Invalid username or password";
            return;
        }

        // FIX: Added the Role claim here so [Authorize(Roles="Admin")] works
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName),
            new(ClaimTypes.Role, AppConstants.AdminRole) 
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var context = HttpContextAccessor.HttpContext!;
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        
        Navigation.NavigateTo(ReturnUrl ?? "/admin", forceLoad: true);
    }
}
EOF

# 2. Update Program.cs to configure the AccessDeniedPath
# ----------------------------------------------------
# We change the default '/Account/AccessDenied' to '/access-denied'
echo "Updating Program.cs..."
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
        options.AccessDeniedPath = "/access-denied"; // FIX: Added explicit Access Denied path
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

    if (telemetryDir is not null)
    {
        var logsPath = Path.Combine(telemetryDir, "logs");
        Directory.CreateDirectory(logsPath);
        logging.AddProcessor(new BatchLogRecordExportProcessor(new FileLogExporter(logsPath)));
    }
});

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await db.Database.MigrateAsync();

    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureAdminUserAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.UseLoginRateLimit();

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
EOF

# 3. Create the AccessDenied.razor page
# -------------------------------------
# This provides a friendly page instead of a 404
echo "Creating AccessDenied.razor..."
cat << 'EOF' > src/MyBlog.Web/Components/Pages/AccessDenied.razor
@page "/access-denied"

<PageTitle>Access Denied</PageTitle>

<div class="container" style="max-width: 600px; margin-top: 50px; text-align: center;">
    <h1 style="color: var(--color-danger);">Access Denied</h1>
    <p>You do not have permission to view this resource.</p>
    <div style="margin-top: 20px;">
        <a href="/" class="btn btn-primary">Return Home</a>
        <a href="/logout" class="btn btn-link">Logout</a>
    </div>
</div>
EOF

echo "=============================================="
echo "  Fix Complete."
echo "=============================================="
echo "  1. Rebuild: dotnet build src/MyBlog.slnx"
echo "  2. Deploy/Run."
echo "  3. IMPORTANT: You must LOG OUT and LOG IN again for the new Role claim to apply."
