Purpose & context
Kushal is building MyBlog, a .NET 10 Blazor Server blogging platform following Clean Architecture principles. The project is a multi-project solution (MyBlog.Core, MyBlog.Infrastructure, MyBlog.Web, MyBlog.Tests, MyBlog.E2E) with SQLite storage, cookie-based authentication, SignalR-based real-time reader tracking, OpenTelemetry observability, and a custom Markdown parser. The application deploys to IIS via WebDeploy through GitHub Actions, with two deployment targets using separate secrets. Kushal develops on both Fedora (Linux) and Windows, with E2E tests running via Podman Compose locally and Docker in CI.
The project originated as a from-scratch generation via shell script and has evolved through iterative feature additions, bug fixes, test coverage expansion, and infrastructure improvements. Key architectural decisions include: no third-party CSS frameworks, no npm/Node dependencies, custom Markdown parsing, images stored as BLOBs in SQLite, XDG-compliant database paths, and OpenTelemetry with OTLP export to Honeycomb.io using only vendor-neutral packages (no Honeycomb SDK).

Current state

Observability: OpenTelemetry integrated with OTLP exporter sending traces/metrics/logs to Honeycomb.io via x-honeycomb-team header auth; conditionally enabled only when endpoint and API key are configured; per-deployment-target secrets in GitHub Actions
E2E testing: Playwright + xUnit v3 suite running in Podman/Docker; infrastructure includes stale container cleanup, optimized Docker layer ordering to avoid redundant browser downloads, and offline support flags
Authentication: Login uses a minimal API POST endpoint (/account/login) with antiforgery token; LoginRateLimitMiddleware applies progressive delays (never fully blocks) and is disabled in Development environments
Database schema: Managed via EnsureCreatedAsync() + DatabaseSchemaUpdater for incremental updates on existing deployments; no EF Core migrations
Test suite: Comprehensive unit, integration, and E2E coverage; xUnit v3 patterns with TestContext.Current.CancellationToken; in-memory SQLite for integration tests


On the horizon

RSS feed support (detailed implementation plan exists: new models, IRssFeedService, minimal API endpoint, tests)
Mobile UX improvements and share sheet functionality (Web Share API with clipboard fallback)
Potential future features noted: comments system, search functionality


Key learnings & principles

Forward-compatible solutions over workarounds: Kushal actively rejects deprecated patterns (e.g., insisted on .NET 10-correct @page directive approach for NotFound.razor rather than reverting to deprecated <NotFound> fragment)
Root cause fixes only: Suppress-warning or workaround approaches are explicitly rejected; fix the actual problem
No regressions: Changes must not break existing deployments or test suites; data safety on live deployments is a hard constraint
Blazor SSR vs. interactive rendering: Login/auth uses SSR with proper name attributes and [SupplyParameterFromForm]; interactive admin components use InteractiveServer render mode — mixing these patterns causes breakage
WebDeploy quirks: WEBSITE_NAME secret must be the IIS site name only (not full domain); -enableRule:AppOffline needed to avoid file-in-use errors during deployment; PowerShell backtick line continuations cause argument mangling with complex quoted params — build args as separate variables instead
PowerShell XML traversal: Dot-notation fails on element names containing dots (e.g., system.webServer); use SelectSingleNode() with XPath instead
Cross-platform newlines: StringBuilder.AppendLine() produces CRLF on Windows; normalize newlines before string assertions in tests
Testability via DI: Injectable delay functions (rather than real Task.Delay) make rate-limiting middleware tests run in milliseconds


Approach & patterns

Always provide complete files: Kushal explicitly requires full file contents for every changed file — never diffs or partial snippets
Minimal, targeted changes: No unnecessary modifications; preserve existing coding style, brace placement, and primary constructors
No hallucination: Read every line of provided code carefully before proposing solutions; verify method signatures and class names exist before referencing them
Codebase provided via dump files: Kushal regularly provides full codebase dumps (dump.txt) and log files for analysis; project knowledge search is used to navigate these
Conditional feature flags: New infrastructure features (OTLP export, file logging, database logging) are gated on configuration flags so they're safely off by default
Centralized package management: Directory.Packages.props with MSBuild property variables for version grouping; no version attributes on individual PackageReference elements


Tools & resources

Runtime/framework: .NET 10, Blazor Server, ASP.NET Core minimal APIs
Data: SQLite via EF Core (no migrations), in-memory SQLite for tests
Observability: OpenTelemetry SDK, OpenTelemetry.Exporter.OpenTelemetryProtocol → Honeycomb.io
Testing: xUnit v3, Playwright for .NET, Podman Compose (local), Docker (CI)
CI/CD: GitHub Actions, WebDeploy (MSDeploy) to IIS, custom PowerShell deployment scripts
Dev environment: Fedora (primary Linux), Windows (secondary); cross-platform compatibility required