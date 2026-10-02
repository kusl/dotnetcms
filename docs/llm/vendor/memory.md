Manage project memory

Claude regenerates project memory every evening from your past chats in this project. Only you can see this memory, and it is not shared with other project users.

Purpose & context

Kushal is the sole developer and operator of dotnetcms (also called MyBlog), a self-hosted .NET 10 Blazor Server blog engine hosted at kush.runasp.net with source at github.com/kusl/dotnetcms. The project follows Clean Architecture (Core / Infrastructure / Web layers) with SQLite via EF Core, SignalR, a hand-written Markdown parser, OpenTelemetry observability, and deployment to IIS via GitHub Actions WebDeploy. It is primarily a learning project that also serves as a real deployment across multiple domains.

Kushal works with AI assistance from both Claude and Gemini, reflected in ADR authorship. The project has a comprehensive 63 KB README in the repository (a zero-byte copy at /mnt/project/README.md is a sync artifact only — never treat it as the real file or rewrite it).

Current state

A complete 22-file ADR set (MADR format, Y-statements, index + ADRs 0001–0020 + solid-review.md appendix) has been produced documenting all major architectural decisions, SOLID adherence, deliberate deviations, accepted nits, and prioritized recommendations
Flaky E2E test race condition (AdminNavigation_CanNavigateBetweenPages) diagnosed and fixed: root cause is Blazor prerender/SignalR circuit gap; fix involves WaitForBlazorInteractiveAsync() extension + data-blazor-interactive attribute on MainLayout.razor; ThemeSwitcherTests.cs and the production-side navigation race were intentionally left for a separate ADR
Post body editor silent-save bug fixed (three compounding defects: onchange vs oninput binding, prerender data loss, unconditional _content write); SignalR MaximumReceiveMessageSize raised to configurable 1 MB; PostRepository.UpdateAsync flagged for incorrectly marking User entity Modified on every post save
CVE-2025-6965 (SQLitePCLRaw.lib.e_sqlite3) resolved via transitive pin to SQLitePCLRaw.bundle_e_sqlite3 3.0.3 in Directory.Packages.props using CentralPackageTransitivePinningEnabled
RSS 2.0 feed live at /feed.xml with full <content:encoded> CDATA for offline reading, output caching, RFC 822 dates, atom:link rel="self", guid isPermaLink="true"
OpenTelemetry integrated via vendor-neutral OTLP exporter to Honeycomb.io; conditionally enabled when both Otlp:Endpoint and Otlp:ApiKey are non-empty; per-deployment secrets injected via GitHub Actions

On the horizon

URL scheme allow-listing in the Markdown parser (flagged as the top security recommendation in solid-review.md — only item with real security value)
Production-side Blazor prerender/navigation race (intentionally deferred; warrants an ADR before touching)
PostRepository.UpdateAsync incorrectly marks User entity as Modified on every post save — latent bug to address
Mobile UX improvements and share sheet functionality (Web Share API with clipboard fallback for Chrome on iOS)

Key learnings & principles

Blazor prerender/circuit gap is a recurring failure mode: any interactive action (link click, form fill) must wait for the SignalR circuit to be live; WaitForBlazorInteractiveAsync() is the established pattern
EnsureCreatedAsync() + DatabaseSchemaUpdater is the schema strategy (no EF Core migrations); CREATE TABLE IF NOT EXISTS for incremental updates; idempotent and safe across deployments
SignalR MaximumReceiveMessageSize defaults to 32 KB and silently aborts the circuit for large documents — must be raised for content-heavy editors
Login must use a minimal API endpoint (POST /account/login) rather than Blazor interactive form handling, because HttpContext is null in interactive SignalR mode; this pattern is established and must not be reverted
Playwright selector patterns: use .Or() for OR conditions, never comma-separated text selectors; use WaitForURLAsync started before click for navigation assertions; avoid RunAndWaitForNavigationAsync (obsolete)
WebDeploy: WEBSITE_NAME secret must be the IIS site name only (not the full domain); -enableRule:AppOffline prevents ERROR_FILE_IN_USE; PowerShell XPath (SelectSingleNode) required for XML element names containing dots
Rate limiting philosophy: progressive delays, never lockout — unlimited attempts always permitted
README zero-byte artifact: /mnt/project/README.md is never the real file; the real README is in the repository at 63 KB
