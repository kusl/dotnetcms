# E2E testing with Playwright

The E2E suite (`src/MyBlog.E2E`) drives Chromium against a running MyBlog instance using Playwright for .NET and xUnit v3.

## Running

### Containers (Podman, matches CI behaviour)

```bash
./run-e2e.sh             # build images, start MyBlog, run tests, clean up
./run-e2e.sh --no-build  # reuse existing images
./run-e2e.sh --clean     # remove E2E containers, networks and volumes
```

`run-e2e.sh` always rebuilds by default; unchanged layers come from the cache, so you never test a stale image by accident. It starts `myblog-web`, waits up to 120 seconds for it to answer, then runs `myblog-e2e` with `--no-deps`. Starting the test container with its dependency would make podman-compose restart `myblog-web`, which produced the old `crun exec.fifo` error. The script exits with the test container's own exit code, read with `podman inspect`. Logs from both containers are written to `test-results/`, and everything is torn down even if a step fails.

The E2E image is based on the .NET SDK image, which already ships PowerShell, curl, git and wget. Playwright bundles its own Node.js runtime. Browsers are installed with the `playwright.ps1` that ships inside the restored `Microsoft.Playwright` package. That layer is rebuilt only when `Directory.Packages.props` or a project file changes, and the build fails if the script cannot be found.

### Locally without containers

```bash
cd src
dotnet build
pwsh "$(find MyBlog.E2E/bin -name playwright.ps1 -print -quit)" install chromium --with-deps
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5000 dotnet run --project MyBlog.Web --no-build &
MYBLOG_BASE_URL=http://localhost:5000 dotnet run --project MyBlog.E2E --no-build
```

| Variable | Default | Purpose |
|----------|---------|---------|
| `MYBLOG_BASE_URL` | `http://localhost:5000` | Instance under test |
| `PLAYWRIGHT_HEADLESS` | `true` | Set `false` to watch the browser (adds 100 ms slow-mo) |

Login rate limiting is disabled in `Development`, which the E2E environments use.

## Waiting for the Blazor circuit

Every page arrives twice: first as prerendered HTML, then from the interactive circuit once SignalR connects. `GotoAsync` and `WaitForLoadStateAsync` only see the first delivery.

Two things go wrong if a test acts in that gap:

- **Link clicks** are handled by enhanced navigation. The URL changes, but the connecting circuit then renders the previous route, so the address bar and the markup disagree.
- **Typed input is lost**, because attaching the circuit replaces the prerendered DOM.

`MainLayout` renders `data-blazor-interactive="true"` once `RendererInfo.IsInteractive` is true. The helpers in `BlazorPageExtensions` build on it:

- `WaitForBlazorInteractiveAsync()` waits for the marker. Call it before clicking links or filling forms. Assertions on prerendered content need no wait.
- `SubmitLoginAsync(username, password, expectedUrl)` opens `/login`, waits for the circuit, fills the form, submits and waits for the expected URL.
- `LoginAsAdminAsync()` logs in with the seeded admin account.

```csharp
await page.GotoAsync("/");
await page.WaitForBlazorInteractiveAsync();
await page.ClickAsync("nav a[href='/about']");
await page.WaitForURLAsync("**/about");
```

## Writing tests

- Put tests in `[Collection(PlaywrightCollection.Name)]` so they share one browser.
- Use the helpers above instead of hand-rolled login code.
- Assert unconditionally. A test that skips its assertion when an element is missing can never fail.
- Use `.Or()` for alternative locators, never comma-separated text selectors.
- Start `WaitForURLAsync` together with the click that navigates (`Task.WhenAll`).
