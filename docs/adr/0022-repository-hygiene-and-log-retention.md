---
status: accepted
date: 2026-10-25
deciders: Kushal (with AI assistance from Claude)
tags: [maintenance, telemetry, testing, ci]
---

# 0022 — Repository hygiene, working local logs, and bounded retention

## Context and Problem Statement

A cleanup review found:

- Silent failures: the file and database log exporters were constructed but never attached to the logging pipeline (since `cbc15ad`, 2026-01-26).
- Tests that could not fail: RSS tests exercised a copy of the endpoint code, and several E2E tests had assertions inside `if` branches.
- Dead code and duplicated configuration.

The app runs on a free host with 5 GB of storage, so enabling local logs requires hard limits on their size and age.

## Decision Outcome

**Logging and retention**
- `TelemetryLoggingExtensions.AddLocalExporters` attaches `FileLogExporter` and `DatabaseLogExporter` through `BatchLogRecordExportProcessor`. The exporters are no longer DI singletons or no-op hosted services.
- `TelemetryCleanupService` runs at startup and hourly. It deletes database rows older than `RetentionDays` (clamped to 1–30) and trims the table to `MaxDatabaseRows` (default 50,000). It deletes `logs_*.json` files older than the cutoff and the oldest files beyond `MaxLogFileMegabytes` (default 100), always keeping the newest file. Database and file cleanup are isolated from each other, and failures are logged.
- Database and log directories share one platform-path implementation (`DatabasePathResolver.GetPreferredDataDirectory`).

**Tests**
- RSS tests call `RssEndpoints.BuildFeedAsync`, the code the endpoint uses.
- New tests cover the exporter pipeline end to end (logger → `TelemetryLogs` row; file exporter → valid JSON array), database trimming, and file retention.
- E2E login goes through `SubmitLoginAsync`/`LoginAsAdminAsync`, which wait for the circuit before typing. Conditional assertions were made unconditional, and tests duplicated across classes were removed.

**Dead code removed**
- `CreatePostDto`, `UpdatePostDto`, `IAuthService.ResetPasswordAsync`, `ITelemetryLogRepository.WriteAsync`/`GetRecentAsync`, the parameterless `PasswordService` constructor, and the image warmer's table-existence probe.
- The duplicate theme bootstrap script, unused JavaScript helpers, page-level `@rendermode` (set globally on `Routes`), and unused `_Imports` usings.
- `Microsoft.Testing.Extensions.TrxReport`; `Microsoft.Extensions.Logging` in Core (replaced by `.Abstractions`); redundant `RootNamespace`/`IsTestProject`.
- From the E2E image: Node.js/npm (Playwright bundles node), the Debian 12 PowerShell repository (the Ubuntu 24.04 SDK image ships `pwsh`), redundant restores, and the `net10.0` fallback path. Also the unused test-results volume, `src/.gitkeep`, and `docs/playwright.md`.
- All code comments. Rationale lives in ADRs and docs.

**Single source of truth**
- One `DOTNET_VERSION` in the workflow. One deploy job with a two-target matrix (`fail-fast: false`) replaces two copies. Deploys run from `main` only.
- Playwright's installer is located from the restored package, so a version bump touches only `Directory.Packages.props`.
- `src/.dockerignore` keeps host `bin/`/`obj/` out of image builds.

**Scripts**
- `run-e2e.sh` builds by default (cached), runs the test container with `--no-deps`, exits with that container's exit code, and always captures logs and cleans up. `--clean` no longer runs a system-wide `podman system prune`.
- `export.sh` writes a compact `dump.txt` and a code-only `slim.md`, and prints only a summary.

### Consequences

- Good: local logs work as documented, and their age and size are bounded. Failures in exporters, retention, feeds and E2E runs are now visible.
- Neutral: database logging adds a batched write every few seconds to the application database (ADR-0015). SQLite reuses freed pages but does not shrink the file.
- Neutral: deploy check names are now `deploy (primary)` and `deploy (nice)`.

**Y-statement:** In the context of a free-tier host with 5 GB of storage, facing silently disabled logging, tests that could not fail, and accumulated dead code, we decided for wired local exporters with hourly age-and-size retention, tests that exercise production code, and aggressive removal of dead code and duplicated configuration, and neglected deleting the exporters outright, to achieve working, bounded diagnostics and loud failures, accepting extra SQLite writes, because the host cannot afford unbounded logs and silent failures had already gone unnoticed for months.

> **Amendment (2026-10-25):** running `playwright.ps1` from the NuGet cache fails, because it loads `Microsoft.Playwright.dll` from its own folder and the package's `build/` folder has no DLL. Building before `COPY . .` also fails, because there is no source yet (CS5001). The E2E image now builds a throwaway `PlaywrightInstaller` project that references the central `Microsoft.Playwright` version, runs the installer from its output, and fails if Chromium is not installed. Browser caching and the single version source are both kept.
