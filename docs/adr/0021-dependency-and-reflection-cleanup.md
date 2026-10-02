---
status: accepted
date: 2026-10-02
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [dependencies, performance, reflection, maintenance]
---

# 0021 — Dependency, reflection, and static-asset cleanup

## Context and Problem Statement

Dependencies and framework APIs drifted as the project moved to .NET 10. A review found stale packages, reflection on hot paths, and a few latent bugs. What do we keep, replace, or remove?

## Decision Outcome

**Dependencies**
- `MyBlog.Infrastructure` uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />` instead of `Microsoft.AspNetCore.Identity` 2.3.x and `Microsoft.Extensions.Http`. The 2.x package pulled ASP.NET Core 2.x assemblies; at runtime the shared framework already won, so hashing is unchanged.
- Removed from `Directory.Packages.props`: `Microsoft.AspNetCore.Identity`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Hosting`, `Microsoft.EntityFrameworkCore.Design`, `xunit.runner.visualstudio` (all unreferenced or replaced) and the `System.Security.Cryptography.Xml` transitive pin (only the 2.x Identity graph needed it).

**Reflection**
- Removed: `FileLogExporter` uses a source-generated `TelemetryJsonContext`; `DatabaseLogExporter` writes attributes with `Utf8JsonWriter` (primitives, dates and GUIDs native, everything else as invariant `ToString`); `PostDetail` JSON-LD uses `JsonObject`; `UseLoginRateLimit` constructs the middleware directly instead of `UseMiddleware<T>` + `[ActivatorUtilitiesConstructor]`.
- Kept: EF Core model building, Blazor parameter binding and routing, DI, SignalR hub dispatch, and the one-time `Assembly.GetName().Version` at startup. These are framework-inherent or off the hot path.

**Framework APIs**
- `MapStaticAssets` + `@Assets[...]` replace `UseStaticFiles`, giving build-time compression, fingerprinting and immutable caching.
- `ChangePassword` reads the user from `AuthenticationStateProvider`; `AddHttpContextAccessor` is removed.

**Fixes**
- `ImageDimensionService` reads GIF/JPEG dimensions and JPEG segment lengths as unsigned 16-bit values (large images, and APP segments ≥ 32 KB no longer misparse); dropped per-call `sqlite_master` checks; `HttpClient` timeout and user agent are set at registration.
- `MarkdownService` looks up dimensions with the decoded URL, so query-string URLs fetch correctly and share the warmer's cache key.
- `PostRepository.UpdateAsync` saves tracked posts via change tracking and attaches detached posts without marking their author graph modified.
- `About` counts published posts with `GetPublishedCountAsync` instead of loading them all.
- Deleted the dead `src/.github/workflows/build-deploy.yml`.

### Consequences

- Good: smaller dependency graph, no stale 2.x packages, no reflection-based JSON in logging or page rendering, precompressed assets.
- Neutral: `TelemetryLogs.Properties` writes non-primitive attribute values (including enums) as strings.
- Neutral: `MapStaticAssets` requires the build-generated `MyBlog.Web.staticwebassets.endpoints.json`, which `dotnet publish` emits.

## More Information

Amends ADR-0008, ADR-0010, ADR-0011 and ADR-0017. Open recommendations stay in `solid-review.md`.

**Y-statement:** In the context of an ageing dependency graph, facing stale packages and reflection on hot paths, we decided for framework references, source-generated or writer-based JSON and `MapStaticAssets`, and neglected keeping the 2.x packages and reflection serializers, to achieve a smaller, faster, auditable build, accepting string serialization of non-primitive log attributes, because no reader depends on their structure.
