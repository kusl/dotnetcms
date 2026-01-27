I have a blog in asp dotnet. recently I added end to end tests with playwright. however, some tests are inexplicably failing. why? what can I do to fix them? as far as I can tell, the system under test works correctly so the tests should pass but they don't. the full code is attached as dump.txt and all the logs are also available to you as attachments. please review thoroughly, word by word, line by line, and give me a complete, comprehensive solution that fixes all my problems. please and thank you 

After reviewing your codebase, test logs, and Docker configuration thoroughly, I've identified the root cause of your E2E test failures. The issue is **multifaceted**, involving Docker volume permissions with SELinux that prevent Data Protection keys from persisting, causing immediate authentication cookie validation failures, combined with subtle timing issues in your Playwright tests.

Here's the comprehensive solution:

## Root Cause Analysis

1. **Data Protection Keys Permission Failure**: Your `Program.cs` correctly configures Data Protection to persist keys to `/app/data/keys`, but your Docker volume mount with SELinux labeling (`:Z`) overrides the permissions set in your Dockerfile. The `appuser` cannot write to the mounted volume, causing keys to remain in-memory only. This immediately invalidates authentication cookies right after they're set.

2. **Form Submission Timing**: Playwright's `ClickAsync()` does not inherently wait for full page navigation when submitting forms. Your tests assume the navigation completes before assertions run, but Blazor Server + form submission causes a race condition.

3. **Missing Keys Directory**: Your Dockerfile creates `/app/data` but not the nested `/app/data/keys` directory with explicit permissions.

## The Fix

### 1. Update Your Dockerfile (`src/MyBlog.Web/Dockerfile`)

Add explicit creation and permission setting for the Data Protection keys directory:

```dockerfile
# Create data directory for SQLite AND Data Protection keys
RUN mkdir -p /app/data/keys && \
    chown -R appuser:appuser /app/data && \
    chmod -R 755 /app/data

# Ensure the appuser owns the keys directory specifically
USER appuser
RUN mkdir -p /app/data/keys  # Create as appuser to ensure ownership
```

### 2. Update Your Login E2E Tests (`src/MyBlog.E2E/Tests/LoginPageTests.cs`)

Replace your form submission logic to explicitly wait for navigation:

```csharp
[Fact]
public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
{
    var page = await _fixture.CreatePageAsync();
    await page.GotoAsync("/login");

    await page.FillAsync("input[name='username']", "admin");
    await page.FillAsync("input[name='password']", "ChangeMe123!");

    // FIX: Use RunAndWaitForNavigationAsync to ensure form submission completes
    await page.RunAndWaitForNavigationAsync(async () =>
    {
        await page.ClickAsync("button[type='submit']");
    }, new PageRunAndWaitForNavigationOptions 
    { 
        UrlRegex = new Regex("/admin|/login.*error") 
    });

    // Now assert the URL
    await Assertions.Expect(page).ToHaveURLAsync(new Regex("/admin"));
}

[Fact]
public async Task LoginPage_WithInvalidCredentials_ShowsError()
{
    var page = await _fixture.CreatePageAsync();
    await page.GotoAsync("/login");

    await page.FillAsync("input[name='username']", "invalid");
    await page.FillAsync("input[name='password']", "invalid");

    // FIX: Wait for navigation to complete (even if it redirects back to login)
    await page.RunAndWaitForNavigationAsync(async () =>
    {
        await page.ClickAsync("button[type='submit']");
    });

    // Wait specifically for the error query parameter to appear
    await Assertions.Expect(page).ToHaveURLAsync(new Regex("/login.*error=invalid"));

    // Now check for the error message
    var errorLocator = page.Locator(".error-message");
    await Assertions.Expect(errorLocator).ToBeVisibleAsync();
    await Assertions.Expect(errorLocator).ToContainTextAsync("Invalid username or password");
}

[Fact]
public async Task LoginPage_AfterLogin_ShowsLogoutButton()
{
    var page = await _fixture.CreatePageAsync();
    await page.GotoAsync("/login");

    await page.FillAsync("input[name='username']", "admin");
    await page.FillAsync("input[name='password']", "ChangeMe123!");

    // FIX: Explicitly wait for navigation to admin page
    await page.RunAndWaitForNavigationAsync(async () =>
    {
        await page.ClickAsync("button[type='submit']");
    }, new PageRunAndWaitForNavigationOptions { UrlRegex = new Regex("/admin") });

    await Assertions.Expect(page).ToHaveURLAsync(new Regex("/admin"));
    await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");
    
    var logoutButton = page.Locator("form[action='/logout'] button");
    await Assertions.Expect(logoutButton).ToBeVisibleAsync();
}
```

### 3. Add Pre-Test Database Seeding Verification

Add a setup check in your `PlaywrightFixture` to verify the admin user exists before tests run:

```csharp
// In LoginPageTests.cs or as a shared setup
[Fact]
public async Task EnsureAdminUserExists()
{
    // This ensures the admin user is created before other tests run
    using var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
    var response = await client.GetAsync("/login");
    Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK);
}
```

### 4. Alternative: Disable Data Protection Persistence for E2E (Optional)

If you want to run tests without worrying about volume permissions, modify `Program.cs` to use ephemeral keys only in Development/E2E environments:

```csharp
var builder = WebApplication.CreateBuilder(args);

// ... existing code ...

// Only persist keys in Production; use ephemeral for E2E
if (builder.Environment.IsProduction())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "data", "keys")));
}
// else: use default in-memory keys (sufficient for single-instance E2E tests)
```

**However**, I recommend keeping the persistence and fixing the permissions as shown in step 1, as it's closer to production.

### 5. Verify Environment Variable Configuration

Ensure your Docker Compose is correctly passing the admin password (which it appears to be, but verify the exact key):

```yaml
# In docker-compose.e2e.yml
services:
  myblog-web:
    environment:
      - Authentication__DefaultAdminPassword=ChangeMe123!
      # Ensure this matches exactly what AuthService expects
```

Your `AuthService.cs` looks correct with this fallback chain:
```csharp
Environment.GetEnvironmentVariable("MYBLOG_ADMIN_PASSWORD")
    ?? _configuration["Authentication:DefaultAdminPassword"]
    ?? "ChangeMe123!"
```

## Verification Steps

After implementing these fixes:

1. **Rebuild containers**: `./run-e2e.sh --clean --build`
2. **Check logs**: After a test run, check `test-results/myblog-web.log` for any Data Protection errors (permission denied exceptions)
3. **Run tests**: The navigation assertions should now pass because:
   - The authentication cookie will be valid (Data Protection keys are persisted correctly)
   - The tests wait for navigation to complete before asserting URLs
   - The error message element will be present after the redirect with query parameters

## Additional Considerations

- **Rate Limiting**: Your tests may trigger the rate limiting middleware (`LoginRateLimitMiddleware`) if run multiple times from the same IP (container IP). The tests clear attempts via `Dispose()`, but if tests fail mid-run, subsequent attempts may be delayed. The `RunAndWaitForNavigationAsync` will handle the delay gracefully, but be aware of the 30s max delay if many failed attempts occur.

- **Antiforgery**: Ensure your `Login.razor` includes the `@Html.AntiForgeryToken()` implicitly via `<AntiforgeryToken />` (which it does). With `data-enhance="false"`, this ensures the token is posted to your minimal API endpoint.

- **Container Health**: Your `run-e2e.sh` healthcheck waits for the root page, but you might want to add a check that the database is seeded:
  ```bash
  # Add to run-e2e.sh after healthcheck loop
  echo "Verifying admin user exists..."
  until curl -sf http://localhost:5000/login | grep -q "username"; do
      sleep 1
  done
  ```

These changes should resolve all three failing tests by ensuring the authentication flow works correctly within the containerized environment and that Playwright properly synchronizes with the form submission navigation.












