# MyBlog

> **AI-assisted development notice:** this project contains code written with help from Large Language Models (Claude by Anthropic and Gemini by Google). Human review and testing have been applied, but treat the code as experimental and use caution in production.

A lightweight, self-hosted blog engine built with .NET 10 and Blazor Server, following Clean Architecture. Free and open source.

**Live:** [kush.runasp.net](https://kush.runasp.net) · **Source:** [github.com/kusl/dotnetcms](https://github.com/kusl/dotnetcms)

## Features

- Markdown posts rendered by a small hand-written parser (headings, emphasis, links, images, inline and fenced code, blockquotes, lists, horizontal rules, tables). All HTML in posts is escaped. See [Writing posts](#writing-posts).
- Images stored as BLOBs in SQLite (JPEG, PNG, GIF, WebP, up to 5 MB), served from `/api/images/{id}`. Image width and height are probed once and cached to prevent layout shift.
- Admin area: dashboard, post editor with live preview, image library, user management, password change.
- Live "people reading now" counts through SignalR.
- RSS 2.0 feed at `/feed.xml` with full post content.
- Six themes (light, dark, sepia, nord, solarized light, dracula), persisted in `localStorage`.
- SEO metadata: Open Graph, Twitter cards, JSON-LD, canonical URLs.
- Cookie authentication with login rate limiting that slows attackers down but never locks anyone out.
- OpenTelemetry traces, metrics and logs, exported over vendor-neutral OTLP when configured, plus local file and database logs with strict retention.

## Architecture

```
src/
├── MyBlog.Core            Domain models, interfaces, MarkdownService, SlugService (no infrastructure dependencies)
├── MyBlog.Infrastructure  EF Core + SQLite, repositories, auth, image probing, telemetry exporters and cleanup
├── MyBlog.Web             Blazor Server UI, minimal API endpoints, SignalR hub, login rate limiting
├── MyBlog.Tests           xUnit v3 unit and integration tests (in-memory SQLite)
└── MyBlog.E2E             Playwright end-to-end tests
```

Dependencies point inward: Web → Infrastructure → Core. Design decisions are recorded as ADRs in [`docs/adr`](docs/adr/README.md).

Notable choices:

- **No EF Core migrations.** `EnsureCreated` builds a new database; `DatabaseSchemaUpdater` adds newer tables to existing databases idempotently (ADR-0005).
- **Login is a static form** that posts to `POST /account/login`, because `HttpContext` is not available inside an interactive circuit (ADR-0013).
- **All routes are Interactive Server**, set once on `Routes` in `App.razor`. Pages are prerendered first. `MainLayout` exposes `data-blazor-interactive` so tests can wait for the circuit.

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/kusl/dotnetcms.git
cd dotnetcms/src
dotnet run --project MyBlog.Web
```

The first start creates the database and an `admin` user with password `ChangeMe123!`. To choose a different initial password, set `MYBLOG_ADMIN_PASSWORD` (or `Authentication:DefaultAdminPassword`) before the first run. The setting is only read when no user exists; change the password afterwards at `/admin/change-password`.

With containers:

```bash
podman-compose up --build
```

## Writing posts

Posts are written in Markdown. The parser is hand-written (no third-party library, ADR-0006) and supports this subset:

| Syntax | Result |
|--------|--------|
| `#` to `######` | Headings |
| `**bold**`, `__bold__`, `*italic*`, `_italic_` | Emphasis. Underscores inside words (`snake_case`) are left alone. |
| `` `code` `` | Inline code, rendered literally |
| ```` ``` ```` fences | Code blocks |
| `[text](url)`, `![alt](url)` | Links and images. Images get `width`/`height` from the dimension cache. |
| `> text` | Single-line blockquote |
| `- item`, `* item` | Unordered list |
| `1. item` | Ordered list. A list starting at another number keeps it (`5.` renders as 5). |
| `---`, `***`, `___` | Horizontal rule |
| Pipe tables | Tables (below) |

Tables use GitHub syntax:

```markdown
| Term | Meaning | Source |
|:-----|:-------:|-------:|
| **SAM.gov** | Entity registration | [link](https://sam.gov) |
| Escaped \| pipe | Centered | Right-aligned |
```

- The second line is required. Its cell count must match the header, or both lines render as plain text.
- Outer pipes are optional. Use `\|` for a literal pipe in a cell, including inside inline code.
- `:--` left, `:-:` center, `--:` right. Alignment is an inline style, so it also works in RSS readers.
- A table ends at a blank line or at a heading, list, blockquote, rule or code fence. Leave a blank line before any following paragraph.
- Short rows are padded with empty cells; extra cells are ignored.
- Cells support links, images, emphasis and inline code. HTML is escaped.
- Tables fill the content width. When they don't fit, for example on a phone, they scroll horizontally inside a bordered box and the page itself does not.

Raw HTML, nested lists, multi-line blockquotes, reference links and setext headings are not supported. Link and image URLs are not scheme-filtered, which is safe only because every author is an admin (ADR-0007).

## Testing

```bash
cd src
dotnet run --project MyBlog.Tests/MyBlog.Tests.csproj
```

The test projects use Microsoft.Testing.Platform (`src/global.json`). Integration tests use in-memory SQLite, so they run the same on Windows, Linux and macOS.

End-to-end tests run against a real instance in containers:

```bash
./run-e2e.sh             # build images (cached layers) and run the suite
./run-e2e.sh --no-build  # reuse existing images
./run-e2e.sh --clean     # remove E2E containers, networks and volumes
```

The script exits with the test container's exit code and writes both containers' logs to `test-results/`. See [`docs/E2E-TESTING.md`](docs/E2E-TESTING.md).

`run.sh` runs format, restore, clean, build, unit tests, package listings, `export.sh` and the E2E suite. It logs everything to `docs/llm/output/` and exits non-zero if any step failed.

## Configuration

Settings live in `src/MyBlog.Web/appsettings.json` and can be overridden by environment variables (`Section__Key`).

| Key | Default | Purpose |
|-----|---------|---------|
| `ConnectionStrings:DefaultConnection` | `Data Source=myblog.db` | SQLite connection. The default value is redirected to the platform data directory (below). |
| `Application:Title` | `MyBlog` | Site title |
| `Application:PostsPerPage` | `10` | Home page size |
| `Application:RequireHttps` | `false` | Marks the auth cookie Secure-only |
| `Application:GitForgeUrl` | repository URL | Footer source link (http/https only) |
| `Authentication:SessionTimeoutMinutes` | `30` | Sliding cookie lifetime |
| `Authentication:DefaultAdminPassword` | `ChangeMe123!` | Initial admin password (first run only) |
| `Blazor:MaximumReceiveMessageSizeBytes` | `1048576` | SignalR message limit, so long posts can be saved |
| `Telemetry:EnableFileLogging` | `true` | Write JSON log files |
| `Telemetry:EnableDatabaseLogging` | `true` | Write logs to the `TelemetryLogs` table |
| `Telemetry:RetentionDays` | `30` | Log retention; values above 30 are clamped to 30 |
| `Telemetry:MaxDatabaseRows` | `50000` | Newest database log rows kept regardless of age |
| `Telemetry:MaxLogFileMegabytes` | `100` | Total size budget for JSON log files |
| `Otlp:Endpoint` / `Otlp:ApiKey` | empty | OTLP export, enabled only when both are set |
| `Otlp:Protocol` | `HttpProtobuf` | `HttpProtobuf` or `Grpc` |

### Data locations

| OS | Database | Log files |
|----|----------|-----------|
| Linux | `$XDG_DATA_HOME/MyBlog/myblog.db` (default `~/.local/share`) | `…/MyBlog/telemetry/logs/` |
| macOS | `~/Library/Application Support/MyBlog/myblog.db` | `…/MyBlog/telemetry/logs/` |
| Windows | `%LOCALAPPDATA%\MyBlog\myblog.db` | `…\MyBlog\telemetry\logs\` |

If the preferred directory is not writable, the database falls back to `data/` and logs fall back to `telemetry/logs/`, both next to the application binaries. If neither log location is writable, file logging is skipped.

## Logging and retention

Logs go to the console, to rotating JSON files (`logs_<run>.json`, 25 MB per file), to the `TelemetryLogs` table, and to OTLP when configured. Default levels are `Information`, with `Warning` for `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore`.

`TelemetryCleanupService` keeps local logs bounded. It runs at startup and then every hour. On each run it:

1. Deletes database log rows older than `RetentionDays` (at most 30).
2. Trims the table to the newest `MaxDatabaseRows` rows.
3. Deletes `logs_*.json` files older than `RetentionDays`.
4. Deletes the oldest remaining log files until the total is within `MaxLogFileMegabytes`. The newest file is always kept.

Database and file cleanup run independently, so a failure in one does not skip the other. Failures are logged. SQLite reuses freed pages, so the database file stops growing once retention is in steady state; it does not shrink on its own.

## Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /feed.xml` | RSS 2.0 feed of the 20 newest published posts (cached 10 minutes) |
| `GET /api/images/{id}` | Image bytes |
| `POST /account/login` | Form login (rate limited outside Development) |
| `POST /logout` | Sign out (requires authentication) |
| `/readerHub` | SignalR hub for reader counts |

## Deployment

`.github/workflows/build-deploy.yml`:

1. **build-test**: restore, build and unit tests on Ubuntu, Windows and macOS.
2. **e2e-tests**: builds, starts the app, installs Chromium and runs the Playwright suite.
3. **deploy**: runs on `main` only. It is a matrix with two IIS targets (`primary`, `nice`) that deploy independently (`fail-fast: false`). Each publishes for `win-x86`, injects OTLP settings into `web.config`, and syncs with WebDeploy using `AppOffline` and `DoNotDeleteRule`.

The .NET SDK version used by CI is set once in the workflow's `DOTNET_VERSION`.

Secrets per target (the second target uses the same names with a `NICE_` prefix): `WEBSITE_NAME` (the IIS site name, not the domain), `SERVER_COMPUTER_NAME`, `SERVER_USERNAME`, `SERVER_PASSWORD`, and optionally `HONEYCOMB_API_KEY` and `OTLP_ENDPOINT`.

## Package versions

All NuGet versions are pinned in `src/Directory.Packages.props`. Central Package Management and transitive pinning are enabled. Bumping a package, including Playwright, only requires editing that file: the E2E container installs browsers through a throwaway project that references the centrally pinned `Microsoft.Playwright`, and CI finds `playwright.ps1` in the build output instead of a hard-coded path.

## LLM context files

`export.sh` writes two files to `docs/llm/`:

- `dump.txt`: every tracked file, with a one-line header per file.
- `slim.md`: code and configuration only (no Markdown, no `export.sh`), as fenced blocks labelled with language and path.

## License

MIT License. Copyright (c) 2026.
