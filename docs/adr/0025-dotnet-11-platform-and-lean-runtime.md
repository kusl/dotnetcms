---
status: accepted
date: 2026-11-24
deciders: Kushal (with AI assistance from Claude)
tags: [platform, dependencies, observability, realtime, ci]
---

# 0025 — .NET 11 platform, in-process reader tracking, and native telemetry

## Context and Problem Statement

.NET 11 (SDK 11.0.100, runtime 11.0.0) shipped on 2026-11-10 and the host supports it. While the app targeted `net10.0`, `dotnet list package --outdated` offered 11.0.0 for every `Microsoft.*` package, which cannot be taken without retargeting.

The question was what this project would look like if it were started today on .NET 11, under fixed constraints: no change to stored data, the database schema, URLs, the file layout, or the HTML and RSS that existing readers receive. Search caches and other derived data may be rebuilt only for a stated reason. None is needed here.

The local development machine has only SDK 11.0.100-rc.3. CI uses the latest stable 11.0 SDK.

## Decision Outcome

**Platform**
- All projects target `net11.0` (`Directory.Build.props`). `LangVersion` is removed, so the language version follows the target framework (C# 15).
- `src/global.json` sets `11.0.100-rc.3.26522.148` as the minimum, with `allowPrerelease` and `rollForward: latestFeature`. The RC 3 machine builds with RC 3, and any machine with a stable 11.0 SDK uses that instead. CI does not read this file. It runs from the repository root and installs `11.0.x`.
- Container images move to `sdk:11.0` and `aspnet:11.0`. Playwright 1.65 supports the Ubuntu 26.04 base.

**Dependencies** (Central Package Management, ADR-0017)

| Package | Change | Reason |
|---|---|---|
| `Microsoft.Extensions.Logging.Abstractions` | removed | Part of the `net11.0` shared framework. A direct reference raises NU1510, which is a build error under warnings-as-errors. |
| `Microsoft.AspNetCore.SignalR.Client` | removed | Its only use was the reader-badge loopback (below). |
| `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http` | removed | ASP.NET Core 11 and `System.Net.Http` (since .NET 9) emit semantic-convention spans and metrics natively. |
| `OpenTelemetry.Exporter.Console` | removed | Console exporters ran in every environment (`solid-review.md` recommendation 5). |
| `Microsoft.NET.Test.Sdk` | removed | Only needed for VSTest. Tests run on Microsoft.Testing.Platform through `dotnet run` and `global.json`. |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.14 → 11.0.0 | Matches the target framework. |

**Reader counts.** This supersedes ADR-0014. `ReaderBadge` no longer opens a server-to-self `HubConnection`. The component already runs inside the reader's circuit, so it injects the singleton `IReaderTrackingService`. `Track(slug, onReadersChanged)` returns an `IDisposable`, and disposing it removes the reader. The service notifies the other readers of that post, and each badge rereads the count on its circuit dispatcher. Tracking starts only when `RendererInfo.IsInteractive` is true, so prerendering no longer adds and then removes a reader. A badge whose `Slug` changes moves its registration to the new post. `ReaderHub`, `/readerHub` and `AddSignalR()` are removed. `AddInteractiveServerComponents` registers SignalR for the circuit itself.

This removes one WebSocket and one loopback HTTP connection per reader. It also removes the server's dependency on reaching its own public URL, which a reverse proxy or a host without hairpin routing can block. The counts stay in memory and are per process, as before.

**Telemetry.** Traces and metrics are registered only when OTLP is enabled. Without OTLP the app does not create activities or metric streams that nothing exports. Tracing subscribes to the `Microsoft.AspNetCore` and `System.Net.Http` sources. Metrics subscribe to the ASP.NET Core meters that the instrumentation package enabled (`Hosting`, `Server.Kestrel`, `Http.Connections`, `Routing`, `Diagnostics`) and to `System.Net.Http` and `System.Net.NameResolution`. Logs still go to the console logger, local files, the database and OTLP when configured (ADR-0015, ADR-0022).

**RSS.** `/feed.xml` now loads the 20 newest published posts, including their bodies, in one query (`IPostRepository.GetLatestPublishedAsync`), not one query per post (`solid-review.md` recommendation 7). The feed's items, order and content are unchanged. The `generator` element reports the running major version (`MyBlog/.NET 11`), so it no longer needs editing on each upgrade.

**Smaller modernizations.** `FileLogExporter` locks on `System.Threading.Lock`, and so does `ReaderTrackingService`.

**CI.** The required build matrix is Ubuntu and Windows. macOS runs as a separate `build-test-macos` job with `continue-on-error` and a 30-minute timeout. Nothing depends on that job, so a macOS runner shortage cannot hold up E2E tests or deployment. `actions/setup-dotnet` moves to v6, which only changed the action's internals.

**Not adopted, with reasons**
- *C# 15 union types and closed hierarchies.* No current type models a closed set of alternatives. Adding one to show the feature would be churn.
- *Runtime async.* Still a preview feature in .NET 11.
- *Removing `app.UseAntiforgery()`.* The .NET 11 templates drop it because the new Sec-Fetch-Site CSRF middleware is automatic. Keeping it is harmless, and the `/account/login` and `/logout` endpoints behave as they did on .NET 10.
- *`CacheView`.* It applies to static SSR. Every route here is Interactive Server.
- *Rebuilding the image-dimension cache.* Its URL keys and stored values are unchanged.

### Consequences

- Good: six fewer packages. Upgrades come from the target framework, so `dotnet package list --outdated` is useful again. `run.sh` also runs `--outdated --highest-minor`, which lists only upgrades within the current major version.
- Good: fewer connections and less loopback traffic per reader. Prerendering no longer adds phantom readers.
- Good: no telemetry work when nothing exports it. The E2E `app.log` contains log lines only, without span and metric dumps.
- Neutral: OTLP spans come from the framework's semantic-convention attributes, not the instrumentation package. Enrichment callbacks and filters were not used.
- Neutral: .NET 11 requires x86-64-v2 CPUs. A host below that baseline fails at startup. The host lists .NET 11 as supported.
- Neutral: .NET 11 is an STS release, supported until 2028-11-14, the same end date as .NET 10 LTS.
- Bad: the RC 3 SDK used locally and the stable SDK used in CI can differ in analyzer or compiler diagnostics until the local SDK is updated.

## More Information

Supersedes ADR-0014. Amends ADR-0015, ADR-0017, ADR-0018, ADR-0019 and ADR-0020. Resolves `solid-review.md` recommendations 5, 6 and 7.

**Y-statement:** In the context of moving to .NET 11 and asking what we would build from scratch today, facing packages the framework now provides, a loopback SignalR client, telemetry with no consumer and an N+1 feed query, we decided for `net11.0`, framework-native telemetry, in-process reader tracking and a single feed query, and neglected staying on .NET 10 and adopting C# 15 features for their own sake, to achieve a smaller dependency graph and less runtime work with unchanged data and output, accepting an STS release and a temporary RC/GA SDK split, because each removed piece had a concrete cost and no remaining benefit.
