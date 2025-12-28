#!/bin/bash
# =============================================================================
# Fix Login and Logout Flow - Complete Solution
# =============================================================================
# This script properly fixes:
# 1. Logout 400 Bad Request - adds MapPost("/logout") minimal API endpoint
# 2. Login flow - ensures form works with both SSR and interactive modes
# 3. Interactive render mode - enables Blazor interactivity globally
# =============================================================================
set -euo pipefail

SRC_DIR="src"

echo "=============================================="
echo "  MyBlog: Fix Login/Logout Flow"
echo "=============================================="
echo ""

# =============================================================================
# Step 1: Update Program.cs - Add logout endpoint BEFORE MapRazorComponents
# =============================================================================
echo "[1/4] Updating Program.cs with logout endpoint..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Program.cs"
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Constants;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Telemetry;
using MyBlog.Web.Components;
using MyBlog.Web.Middleware;
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
        .AddProcessor(new FileActivityExporter(TelemetryPaths.GetTracePath())))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddReader(new PeriodicExportingMetricReader(
            new FileMetricExporter(TelemetryPaths.GetMetricsPath()),
            exportIntervalMilliseconds: 60000)));

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddProcessor(new FileLogExporter(TelemetryPaths.GetLogsPath()));
});

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// Start telemetry cleanup service
var cleanupService = app.Services.GetRequiredService<TelemetryCleanupService>();
_ = cleanupService.StartAsync(CancellationToken.None);

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

echo "      Done."

# =============================================================================
# Step 2: Update App.razor - Enable InteractiveServer globally
# =============================================================================
echo "[2/4] Updating App.razor with interactive render mode..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/App.razor"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@(Title ?? "MyBlog")</title>
    <base href="/" />
    <link rel="stylesheet" href="css/site.css" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>

@code {
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private string? Title => HttpContext?.RequestServices
        .GetService<IConfiguration>()?["Application:Title"];
}
EOF

echo "      Done."

# =============================================================================
# Step 3: Update _Imports.razor - Add RenderMode import
# =============================================================================
echo "[3/4] Updating _Imports.razor..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/_Imports.razor"
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using MyBlog.Core.Constants
@using MyBlog.Core.Interfaces
@using MyBlog.Core.Models
@using MyBlog.Web.Components
@using MyBlog.Web.Components.Layout
@using MyBlog.Web.Components.Shared
@using static Microsoft.AspNetCore.Components.Web.RenderMode
EOF

echo "      Done."

# =============================================================================
# Step 4: Update Login.razor - Fix form handling for both SSR and interactive
# =============================================================================
echo "[4/4] Updating Login.razor..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Login.razor"
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
        // Use form values if available (SSR form post), otherwise use bound values
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

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var context = HttpContextAccessor.HttpContext!;
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        Navigation.NavigateTo(ReturnUrl ?? "/admin", forceLoad: true);
    }
}
EOF

echo "      Done."

# =============================================================================
# Summary
# =============================================================================
echo ""
echo "=============================================="
echo "  Login/Logout Flow Fixed!"
echo "=============================================="
echo ""
echo "Changes made:"
echo ""
echo "  1. Program.cs:"
echo "     - Added MapPost('/logout') endpoint BEFORE MapRazorComponents"
echo "     - This handles the POST from MainLayout's logout form"
echo "     - The endpoint signs out and redirects to home"
echo ""
echo "  2. App.razor:"
echo "     - Added @rendermode='InteractiveServer' to Routes and HeadOutlet"
echo "     - This enables Blazor interactive features globally"
echo "     - This also ensures blazor.web.js is served correctly"
echo ""
echo "  3. _Imports.razor:"
echo "     - Added 'using static Microsoft.AspNetCore.Components.Web.RenderMode'"
echo "     - Makes InteractiveServer available without full namespace"
echo ""
echo "  4. Login.razor:"
echo "     - Added name='username' and name='password' attributes"
echo "     - Added [SupplyParameterFromForm] for SSR form POST handling"
echo "     - Uses form values when available, falls back to @bind values"
echo ""
echo "Why the logout was broken:"
echo "  - MainLayout has: <form method='post' action='/logout'>"
echo "  - This is an HTTP POST to /logout"
echo "  - Blazor pages like Logout.razor handle GET requests"
echo "  - Without MapPost('/logout'), the server returned 400 Bad Request"
echo ""
echo "Next steps:"
echo "  1. Run: chmod +x fix-login-logout.sh && ./fix-login-logout.sh"
echo "  2. Build: dotnet build src/MyBlog.slnx"
echo "  3. Run: dotnet run --project src/MyBlog.Web"
echo "  4. Test: Login with admin credentials, then click Logout"
echo ""
EOF
