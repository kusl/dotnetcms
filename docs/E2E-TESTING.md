# MyBlog E2E Testing with Playwright

This document describes how to run End-to-End (E2E) tests for MyBlog using Playwright.

## Overview

The E2E tests verify that the entire application works correctly from a user's perspective, testing real browser interactions against a running instance of MyBlog.

## Prerequisites

### For Local Development (Fedora)

```bash
# Verify podman-compose is installed
podman-compose --version
# Expected: podman-compose version 1.5.0+, podman version 5.7+

# Install .NET 10 SDK if not already installed
sudo dnf install dotnet-sdk-10.0
```

### For GitHub Actions

No additional setup required - the workflow handles everything.

## Running E2E Tests

### Option 1: Using Podman Compose (Recommended for Fedora)

This is the recommended approach as it provides isolation and matches CI behavior.

```bash
# From the repository root
chmod +x run-e2e.sh
./run-e2e.sh

# Force rebuild containers
./run-e2e.sh --build

# Clean up everything
./run-e2e.sh --clean
```

### Option 2: Direct Execution (Development)

For faster iteration during development:

```bash
# Terminal 1: Start MyBlog
cd src/MyBlog.Web
ASPNETCORE_URLS=http://localhost:5000 dotnet run

# Terminal 2: Install Playwright and run tests
cd src/MyBlog.E2E
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
MYBLOG_BASE_URL=http://localhost:5000 dotnet run
```

### Option 3: Using Docker Compose (Alternative)

If you prefer Docker over Podman:

```bash
docker compose -f docker-compose.e2e.yml up --build
```

## Test Structure

```
src/MyBlog.E2E/
├── PlaywrightFixture.cs      # Shared browser fixture
├── PlaywrightCollection.cs   # xUnit collection definition
└── Tests/
    ├── HomePageTests.cs      # Homepage tests
    ├── AboutPageTests.cs     # About page tests
    ├── LoginPageTests.cs     # Authentication tests
    └── ThemeSwitcherTests.cs # Theme switching tests
```

## Epic 1 Test Coverage

The initial E2E tests cover Epic 1 functionality:

| Feature | Tests |
|---------|-------|
| Homepage | Page loads, displays heading, has navigation |
| About Page | Page loads, shows architecture, has reader badge |
| Login | Form displays, invalid credentials show error, valid credentials redirect |
| Theme Switcher | Visible on page, changes theme, has multiple options |

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `MYBLOG_BASE_URL` | `http://localhost:5000` | URL of the MyBlog instance |
| `PLAYWRIGHT_HEADLESS` | `true` | Run browsers in headless mode |

### SELinux (Fedora)

The docker-compose files use `:Z` volume mounts for SELinux compatibility. If you encounter permission issues:

```bash
# Check SELinux status
getenforce

# Set context for test-results directory
chcon -Rt svirt_sandbox_file_t test-results
```

## Troubleshooting

### "MyBlog failed to start within timeout"

1. Check if port 5000 is available:
   ```bash
   sudo ss -tlnp | grep 5000
   ```

2. View container logs:
   ```bash
   podman-compose -f docker-compose.e2e.yml logs myblog-web
   ```

### "Playwright browsers not found"

```bash
# Install browsers manually
playwright install chromium --with-deps
```

### "Permission denied" on Fedora

```bash
# Fix SELinux context
chcon -Rt svirt_sandbox_file_t ./test-results
```

### Tests timeout waiting for elements

Increase the timeout in test code:
```csharp
await Assertions.Expect(element).ToBeVisibleAsync(new() { Timeout = 10000 });
```

## Writing New Tests

### Test Structure

```csharp
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class MyNewTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task MyTest_Description()
    {
        var page = await _fixture.CreatePageAsync();
        
        await page.GotoAsync("/my-page");
        
        var element = page.Locator("selector");
        await Assertions.Expect(element).ToBeVisibleAsync();
    }
}
```

### Best Practices

1. **Use semantic selectors**: Prefer `text=`, role-based, or data-testid selectors over CSS classes
2. **Wait for elements**: Use `WaitForSelectorAsync` or `Expect().ToBeVisibleAsync()`
3. **Handle dynamic content**: Account for SignalR connections and lazy loading
4. **Keep tests independent**: Each test should work in isolation
5. **Use descriptive names**: Test names should describe the scenario

## CI/CD Integration

E2E tests run automatically in GitHub Actions:

1. Unit tests must pass first
2. MyBlog is built and started
3. Playwright browsers are installed
4. E2E tests run against the live instance
5. Test results are uploaded as artifacts

The E2E job only runs on `ubuntu-latest` to ensure consistent browser behavior.
