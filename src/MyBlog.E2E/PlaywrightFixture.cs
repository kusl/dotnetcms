using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E;

/// <summary>
/// Shared Playwright fixture for E2E tests.
/// Provides browser and context instances reused across tests.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Browser not initialized");
    public IPlaywright Playwright => _playwright ?? throw new InvalidOperationException("Playwright not initialized");

    /// <summary>
    /// Base URL for the application under test.
    /// Can be overridden via environment variable for container testing.
    /// </summary>
    public string BaseUrl => Environment.GetEnvironmentVariable("MYBLOG_BASE_URL") ?? "http://localhost:5000";

    public async ValueTask InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        var headless = Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADLESS") != "false";

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = headless ? 0 : 100 // Slow down for debugging when not headless
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    /// <summary>
    /// Creates a new browser context with default settings.
    /// </summary>
    public async Task<IBrowserContext> CreateContextAsync()
    {
        return await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });
    }

    /// <summary>
    /// Creates a new page with a fresh context.
    /// </summary>
    public async Task<IPage> CreatePageAsync()
    {
        var context = await CreateContextAsync();
        return await context.NewPageAsync();
    }
}
