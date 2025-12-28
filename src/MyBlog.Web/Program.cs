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

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

// Configure OpenTelemetry
var serviceName = "MyBlog.Web";
var serviceVersion = "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Configure OpenTelemetry logging
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion));
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddConsoleExporter();
});

var app = builder.Build();

// Initialize database and ensure admin user exists
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await context.Database.EnsureCreatedAsync();

    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureAdminUserAsync();
}

// Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    if (builder.Configuration.GetValue("Application:RequireHttps", false))
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
}

app.UseStaticFiles();
app.UseLoginRateLimit();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Map logout endpoint - handles POST from the logout form in MainLayout
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();

// Image endpoint
app.MapGet("/api/images/{id:guid}", async (Guid id, IImageRepository repo, CancellationToken ct) =>
{
    var image = await repo.GetByIdAsync(id, ct);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Data, image.ContentType, image.FileName);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
