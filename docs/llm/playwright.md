# MyBlog Playwright End-to-End Test Plan

## Overview

This document outlines a comprehensive Playwright test suite for MyBlog that covers complete user journeys ("epics") from fresh database state through all major application features.

---

## Project Structure

```
src/
├── MyBlog.Web/
├── MyBlog.Tests/              # Existing xUnit tests
└── MyBlog.E2E/                # New Playwright project
    ├── MyBlog.E2E.csproj
    ├── playwright.config.ts   # Or .runsettings for .NET
    ├── GlobalSetup.cs         # Fresh DB setup
    ├── Fixtures/
    │   ├── DatabaseFixture.cs
    │   ├── ServerFixture.cs
    │   └── BrowserFixture.cs
    ├── PageObjects/
    │   ├── LoginPage.cs
    │   ├── AdminDashboardPage.cs
    │   ├── PostEditorPage.cs
    │   ├── PostListPage.cs
    │   ├── UserEditorPage.cs
    │   ├── ChangePasswordPage.cs
    │   ├── HomePage.cs
    │   ├── PostDetailPage.cs
    │   └── AboutPage.cs
    ├── Epics/
    │   ├── Epic01_AuthenticationJourney.cs
    │   ├── Epic02_UserManagementJourney.cs
    │   ├── Epic03_ContentManagementJourney.cs
    │   ├── Epic04_PublicBrowsingJourney.cs
    │   ├── Epic05_RateLimitingJourney.cs
    │   └── Epic06_RealTimeReaderTracking.cs
    └── Helpers/
        ├── TestDataGenerator.cs
        └── WaitHelpers.cs
```

---

## Epic 1: Authentication Journey

**Goal:** Verify complete authentication flow from fresh state

### Test Scenarios

| # | Scenario | Steps | Assertions |
|---|----------|-------|------------|
| 1.1 | Fresh DB has default admin | Start app with empty DB | Admin user exists |
| 1.2 | Login with default credentials | Navigate to `/login`, enter `admin`/`ChangeMe123!` | Redirected to `/admin` |
| 1.3 | Admin can change password | Go to `/admin/change-password`, change to `NewSecure123!` | Success message, can login with new password |
| 1.4 | Old password no longer works | Logout, try `ChangeMe123!` | "Invalid username or password" error |
| 1.5 | Login with new password | Enter `NewSecure123!` | Redirected to `/admin` |
| 1.6 | Logout works | Click logout button | Redirected to `/`, no admin link visible |
| 1.7 | Protected routes redirect | Try to access `/admin` while logged out | Redirected to `/login?returnUrl=%2Fadmin` |
| 1.8 | ReturnUrl works after login | Login from redirected state | Lands on `/admin`, not `/` |

### Implementation Notes

```csharp
[TestClass]
public class Epic01_AuthenticationJourney : PageTest
{
    [TestMethod]
    [TestCategory("Epic")]
    public async Task CompleteAuthenticationJourney()
    {
        // 1.1 - Fresh state verified by fixture
        
        // 1.2 - Login with defaults
        await Page.GotoAsync("/login");
        await Page.FillAsync("#username", "admin");
        await Page.FillAsync("#password", "ChangeMe123!");
        await Page.ClickAsync("button[type=submit]");
        await Expect(Page).ToHaveURLAsync("/admin");
        
        // 1.3 - Change password
        await Page.GotoAsync("/admin/change-password");
        await Page.FillAsync("#currentPassword", "ChangeMe123!");
        await Page.FillAsync("#newPassword", "NewSecure123!");
        await Page.FillAsync("#confirmPassword", "NewSecure123!");
        await Page.ClickAsync("button[type=submit]");
        await Expect(Page.Locator(".success-message")).ToBeVisibleAsync();
        
        // ... continue journey
    }
}
```

---

## Epic 2: User Management Journey

**Goal:** Admin creates and manages additional users

### Test Scenarios

| # | Scenario | Steps | Assertions |
|---|----------|-------|------------|
| 2.1 | Navigate to user management | Click "Manage Users" or go to `/admin/users` | User list page loads |
| 2.2 | Create new user | Fill form: `author1`, `Author One`, `author@test.com`, `AuthorPass123!` | User appears in list |
| 2.3 | New user can login | Logout admin, login as `author1` | Redirected to `/admin` |
| 2.4 | New user can change own password | Go to change password, update | Success, can login with new password |
| 2.5 | Admin can edit user | Login as admin, edit `author1`'s display name | Change persists |
| 2.6 | Admin cannot delete self | View user list | Delete button missing/disabled for current user |
| 2.7 | Admin can delete other user | Delete `author1` | User removed from list |
| 2.8 | Deleted user cannot login | Try to login as deleted user | Login fails |

### Data Dependencies

- Requires Epic 1 to complete (admin password changed)
- Creates test user for Epic 3

---

## Epic 3: Content Management Journey

**Goal:** Create, edit, and delete posts as different users

### Test Scenarios

| # | Scenario | Steps | Assertions |
|---|----------|-------|------------|
| 3.1 | Create draft post | New post, title "Draft Post", uncheck Published | Post in list as "Draft" |
| 3.2 | Draft not visible publicly | Logout, go to homepage | Draft post not shown |
| 3.3 | Create published post | New post, "First Published Post", check Published | Post appears on homepage |
| 3.4 | Markdown renders correctly | Create post with `# Heading`, `**bold**`, `- list` | HTML renders properly |
| 3.5 | Edit post title | Change "First Published Post" → "Updated Title" | Slug updates, old URL 404s |
| 3.6 | Edit post content | Add new paragraph | Content updated on public view |
| 3.7 | Unpublish post | Uncheck Published | Post disappears from homepage |
| 3.8 | Republish post | Check Published | Post reappears |
| 3.9 | Delete post | Delete from admin | Post gone from list and 404s publicly |
| 3.10 | Live preview works | Type markdown in editor | Preview pane updates in real-time |

### Bulk Content Tests

| # | Scenario | Steps | Assertions |
|---|----------|-------|------------|
| 3.11 | Create 15 posts | Loop: create posts "Post 01" through "Post 15" | All 15 in admin list |
| 3.12 | Homepage shows 10 | Go to homepage | Exactly 10 posts, pagination visible |
| 3.13 | Page 2 shows remaining | Click "Next" | Shows posts 11-15 |
| 3.14 | Direct page access | Go to `/?page=2` | Same 5 posts |
| 3.15 | Invalid page handled | Go to `/?page=999` | Graceful handling (empty or redirect) |

---

## Epic 4: Public Browsing Journey

**Goal:** Verify public user experience without authentication

### Test Scenarios

| # | Scenario | Steps | Assertions |
|---|----------|-------|------------|
| 4.1 | Homepage loads | Go to `/` | Title, post list visible |
| 4.2 | Post card shows metadata | View homepage | Author name, date, summary visible |
| 4.3 | Click through to post | Click post title | Full post content visible |
| 4.4 | Post has SEO metadata | Inspect `<head>` | og:title, description, canonical URL present |
| 4.5 | About page loads | Go to `/about` | Architecture info, tech stack visible |
| 4.6 | Navigation works | Use nav links | Correct pages load |
| 4.7 | Theme switcher works | Click theme button, select "Dark" | Theme changes, persists on reload |
| 4.8 | 404 for missing post | Go to `/post/nonexistent-slug` | "Post Not Found" message |

---

## Epic 5: Rate Limiting Journey

**Goal:** Verify brute-force protection works

### Test Scenarios

| # | Scenario | Steps | Assertions |
|---|----------|-------|------------|
| 5.1 | First 5 attempts instant | Submit wrong password 5x | No noticeable delay |
| 5.2 | 6th attempt delayed | Submit wrong password | Response takes ~1 second |
| 5.3 | Progressive delay | Continue wrong attempts | Each takes longer (2s, 4s, 8s...) |
| 5.4 | Max delay caps at 30s | 11+ attempts | Delay doesn't exceed 30s |
| 5.5 | Correct password still works | Enter correct password after delays | Login succeeds (after delay) |
| 5.6 | Never fully blocked | Many attempts | Always eventually get response, never 429/403 |

### Implementation Notes

```csharp
[TestMethod]
public async Task RateLimiting_ProgressiveDelays()
{
    var stopwatch = new Stopwatch();
    
    for (int i = 1; i <= 8; i++)
    {
        await Page.GotoAsync("/login");
        await Page.FillAsync("#username", "admin");
        await Page.FillAsync("#password", "wrongpassword");
        
        stopwatch.Restart();
        await Page.ClickAsync("button[type=submit]");
        await Page.WaitForSelectorAsync(".error-message");
        stopwatch.Stop();
        
        if (i <= 5)
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500, $"Attempt {i} should be instant");
        else
            Assert.IsTrue(stopwatch.ElapsedMilliseconds >= (1 << (i - 6)) * 900, 
                $"Attempt {i} should have delay");
    }
}
```

---

## Epic 6: Real-Time Reader Tracking

**Goal:** Verify SignalR-based reader count updates

### Test Scenarios

| # | Scenario | Steps | Assertions |
|---|----------|-------|------------|
| 6.1 | Single reader shows 1 | Open post in browser | Reader badge shows "1" |
| 6.2 | Second browser increments | Open same post in new browser context | Both show "2" |
| 6.3 | Leaving decrements | Close second browser | First shows "1" again |
| 6.4 | Different posts independent | Browser A on Post 1, Browser B on Post 2 | Each shows "1" |
| 6.5 | About page tracking | Open `/about` | Reader badge works there too |
| 6.6 | Reconnection works | Simulate disconnect/reconnect | Count recovers correctly |

### Implementation Notes (Multi-Browser)

```csharp
[TestMethod]
public async Task ReaderCount_MultipleUsers()
{
    // Create two separate browser contexts (like two different users)
    var context1 = await Browser.NewContextAsync();
    var context2 = await Browser.NewContextAsync();
    
    var page1 = await context1.NewPageAsync();
    var page2 = await context2.NewPageAsync();
    
    // First user opens post
    await page1.GotoAsync("/post/test-post");
    await Expect(page1.Locator(".reader-badge")).ToHaveTextAsync("1");
    
    // Second user opens same post
    await page2.GotoAsync("/post/test-post");
    
    // Both should show 2
    await Expect(page1.Locator(".reader-badge")).ToHaveTextAsync("2");
    await Expect(page2.Locator(".reader-badge")).ToHaveTextAsync("2");
    
    // Second user leaves
    await page2.CloseAsync();
    
    // First user should see 1 again
    await Expect(page1.Locator(".reader-badge")).ToHaveTextAsync("1");
    
    await context1.DisposeAsync();
    await context2.DisposeAsync();
}
```

---

## Test Infrastructure

### 1. Database Fixture

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public string DatabasePath { get; private set; }
    
    public async Task InitializeAsync()
    {
        // Create unique temp database for this test run
        DatabasePath = Path.Combine(Path.GetTempPath(), $"myblog-test-{Guid.NewGuid()}.db");
        
        // Optionally pre-seed with known state
    }
    
    public async Task DisposeAsync()
    {
        // Clean up database file
        if (File.Exists(DatabasePath))
            File.Delete(DatabasePath);
    }
}
```

### 2. Server Fixture

```csharp
public class ServerFixture : IAsyncLifetime
{
    private WebApplication? _app;
    public string BaseUrl => "https://localhost:5555";
    
    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new[] 
        { 
            "--urls", BaseUrl,
            "--environment", "Testing"
        });
        
        // Configure to use test database
        builder.Configuration["ConnectionStrings:DefaultConnection"] = 
            $"Data Source={_databaseFixture.DatabasePath}";
        
        // ... rest of normal startup
        
        _app = builder.Build();
        await _app.StartAsync();
    }
    
    public async Task DisposeAsync()
    {
        if (_app != null)
            await _app.StopAsync();
    }
}
```

### 3. Page Object Pattern

```csharp
public class LoginPage
{
    private readonly IPage _page;
    
    public LoginPage(IPage page) => _page = page;
    
    // Locators
    private ILocator UsernameInput => _page.Locator("#username");
    private ILocator PasswordInput => _page.Locator("#password");
    private ILocator SubmitButton => _page.Locator("button[type=submit]");
    private ILocator ErrorMessage => _page.Locator(".error-message");
    
    // Actions
    public async Task GotoAsync() => await _page.GotoAsync("/login");
    
    public async Task LoginAsync(string username, string password)
    {
        await UsernameInput.FillAsync(username);
        await PasswordInput.FillAsync(password);
        await SubmitButton.ClickAsync();
    }
    
    public async Task<bool> HasErrorAsync() => await ErrorMessage.IsVisibleAsync();
    
    public async Task<string> GetErrorTextAsync() => await ErrorMessage.TextContentAsync() ?? "";
}
```

---

## Test Execution Strategy

### Ordering

Epics should run in sequence since they build on each other:

```csharp
[TestClass]
[TestMethodOrder(MethodOrderer.OrderAnnotation)]
public class FullUserJourney
{
    [TestMethod, Order(1)]
    public async Task Epic01_Authentication() { }
    
    [TestMethod, Order(2)]
    public async Task Epic02_UserManagement() { }
    
    [TestMethod, Order(3)]
    public async Task Epic03_ContentManagement() { }
    
    // ...
}
```

### Parallel vs Sequential

| Approach | Pros | Cons |
|----------|------|------|
| Sequential (single DB) | Realistic journey, simpler setup | Slower, tests coupled |
| Parallel (isolated DBs) | Fast, independent | More resources, less realistic |
| Hybrid | Best of both | Complex setup |

**Recommendation:** Run epics sequentially within a journey, but allow multiple journey runs in parallel with isolated databases.

### CI Integration

```yaml
# .github/workflows/e2e.yml
e2e-tests:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Install Playwright
      run: |
        dotnet tool install --global Microsoft.Playwright.CLI
        playwright install chromium
    
    - name: Run E2E Tests
      run: dotnet test src/MyBlog.E2E --logger trx
    
    - name: Upload Screenshots on Failure
      if: failure()
      uses: actions/upload-artifact@v4
      with:
        name: playwright-screenshots
        path: src/MyBlog.E2E/screenshots/
```

---

## Cross-Browser Testing

| Browser | Priority | Notes |
|---------|----------|-------|
| Chromium | High | Primary target |
| Firefox | Medium | Different JS engine |
| WebKit | Medium | Safari compatibility |
| Mobile Chrome | High | Where your bug was found |
| Mobile Safari | Medium | iOS users |

```csharp
[TestMethod]
[DataRow("chromium")]
[DataRow("firefox")]
[DataRow("webkit")]
public async Task CrossBrowser_LoginWorks(string browserType)
{
    var browser = await Playwright[browserType].LaunchAsync();
    // ... test
}
```

---

## Estimated Coverage

| Area | xUnit Coverage | Playwright Coverage |
|------|---------------|---------------------|
| Password hashing | ✅ | - |
| Auth logic | ✅ | ✅ (E2E) |
| Rate limiting | ✅ (timing) | ✅ (real delays) |
| Markdown rendering | ✅ | ✅ (in browser) |
| Form submissions | ❌ | ✅ |
| Enhanced navigation | ❌ | ✅ |
| SignalR real-time | ❌ | ✅ |
| Theme persistence | ❌ | ✅ |
| Mobile behavior | ❌ | ✅ |
| SEO metadata | ❌ | ✅ |

---

## Next Steps

1. **Create `MyBlog.E2E` project** with Playwright NuGet packages
2. **Implement fixtures** for database and server lifecycle
3. **Build page objects** for each major page
4. **Implement Epic 1** as proof of concept
5. **Add to CI pipeline** with screenshot capture on failure
6. **Expand to remaining epics** incrementally
























