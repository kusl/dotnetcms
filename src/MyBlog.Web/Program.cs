using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyBlog.Core.Constants;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Telemetry;
using MyBlog.Web.Components;
using MyBlog.Web.Endpoints;
using MyBlog.Web.Hubs;
using MyBlog.Web.Middleware;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var maxCircuitMessageBytes = builder.Configuration.GetValue(
    "Blazor:MaximumReceiveMessageSizeBytes", 1024 * 1024);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    })
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = maxCircuitMessageBytes;
    });

builder.Services.AddSignalR();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOutputCache();

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

var serviceName = "MyBlog";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];
var otlpApiKey = builder.Configuration["Otlp:ApiKey"];
var otlpEnabled = !string.IsNullOrWhiteSpace(otlpEndpoint) && !string.IsNullOrWhiteSpace(otlpApiKey);

var otlpProtocol = OtlpExportProtocol.HttpProtobuf;
var configuredProtocol = builder.Configuration["Otlp:Protocol"];
if (!string.IsNullOrWhiteSpace(configuredProtocol) &&
    configuredProtocol.Equals("Grpc", StringComparison.OrdinalIgnoreCase))
{
    otlpProtocol = OtlpExportProtocol.Grpc;
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();

        if (otlpEnabled)
        {
            tracing.AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpEndpoint!);
                o.Protocol = otlpProtocol;
                o.Headers = $"x-honeycomb-team={otlpApiKey}";
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();

        if (otlpEnabled)
        {
            metrics.AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpEndpoint!);
                o.Protocol = otlpProtocol;
                o.Headers = $"x-honeycomb-team={otlpApiKey}";
            });
        }
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddConsoleExporter();

    if (otlpEnabled)
    {
        logging.AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(otlpEndpoint!);
            o.Protocol = otlpProtocol;
            o.Headers = $"x-honeycomb-team={otlpApiKey}";
        });
    }
});
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseLoginRateLimit();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseOutputCache();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapPost("/account/login", async (HttpContext context, IAuthService authService) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect($"/login?error=required&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
    }

    var user = await authService.AuthenticateAsync(username, password);
    if (user is null)
    {
        return Results.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username),
        new("DisplayName", user.DisplayName),
        new(ClaimTypes.Role, AppConstants.AdminRole)
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/admin" : returnUrl);
});
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();
app.MapGet("/api/images/{id:guid}", async (Guid id, IImageRepository imageRepository) =>
{
    var image = await imageRepository.GetByIdAsync(id);
    if (image is null)
    {
        return Results.NotFound();
    }
    return Results.File(image.Data, image.ContentType);
});

app.MapRssEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ReaderHub>("/readerHub");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await context.Database.EnsureCreatedAsync();
    await DatabaseSchemaUpdater.ApplyUpdatesAsync(context);

    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureAdminUserAsync();
}

app.Run();
