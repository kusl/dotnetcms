---
status: accepted
date: 2026-01-16
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [content, performance, images, cls]
---

# 0008 — Custom image-dimension probe, cache, and startup warmer

## Context and Problem Statement

Images embedded in posts (both uploaded `/api/images/{id}` images and arbitrary
external URLs) cause **cumulative layout shift (CLS)** if rendered without
`width`/`height`. We want to emit intrinsic dimensions on `<img>` tags without
downloading full images and without an image-processing dependency (ADR-0006).

## Decision Drivers

- Add `width`/`height` to rendered images to eliminate layout shift.
- Do not download entire images — only enough bytes to read the header.
- No `ImageSharp`/`SkiaSharp`-style dependency (ADR-0006).
- Never let dimension resolution break page rendering or crash the app.

## Considered Options

1. **An image library** (ImageSharp/SkiaSharp) to decode images. Rejected by
   ADR-0006 and overkill for reading a header.
2. **Client-side sizing** — let the browser reflow. This *is* the CLS problem.
3. **A hand-written header probe** that reads only the first bytes, plus a DB cache
   and a startup warmer.

## Decision Outcome

Chosen option: **a hand-written probe with cache + warmer** (option 3):

- `ImageDimensionService` issues an HTTP GET with
  `HttpCompletionOption.ResponseHeadersRead`, reads ~32 header bytes, and parses
  intrinsic dimensions for **PNG, GIF, JPEG, and WebP** (VP8/VP8L/VP8X) directly
  from the binary header using `System.Buffers.Binary`. Timeout is 10 s; a custom
  `User-Agent` is sent.
- Results are cached in the `ImageDimensionCache` table (keyed by URL). Every cache
  read/write is wrapped so a *missing table* or any error degrades gracefully — the
  image simply renders without dimensions.
- `ImageCacheWarmerService` (a `BackgroundService`) runs ~5 s after startup,
  streams post content with `AsAsyncEnumerable()` to avoid loading everything into
  memory, extracts image URLs, and pre-populates dimensions for any not yet cached,
  throttled with a 100 ms delay to be gentle on remote servers.

The `MarkdownService` calls the probe during inline image rendering (ADR-0007) and
falls back to a dimensionless `<img>` on any failure.

### Consequences

- Good: correct `width`/`height` on most images with minimal bandwidth (headers
  only); CLS is largely eliminated after the cache warms.
- Good: fully graceful — no code path lets a probe failure break rendering or
  startup; the warmer's errors are explicitly non-fatal.
- Bad: only four formats are understood; anything else (SVG, AVIF, exotic files)
  yields no dimensions and renders unsized.
- Bad: first render of an uncached external image incurs a network round trip
  (bounded by the 10 s timeout); the warmer mitigates this over time.
- Neutral: `ImageDimensionService` is a typed `HttpClient` and uses
  `IServiceScopeFactory` to reach the scoped `BlogDbContext` from its effectively
  non-scoped lifetime — the same pattern used by the background services and
  telemetry exporters.

## More Information

The table's existence is checked with a `sqlite_master` query because a database
created before this feature would not have the table until the schema updater runs
(ADR-0005). AVIF/SVG support would be the natural next extension.

**Y-statement:** In the context of eliminating image layout shift without an image
library, facing the cost of downloading full images, we decided for a hand-written
header probe with a DB cache and a startup warmer and neglected an image-processing
dependency and client-side reflow, to achieve correct dimensions with minimal
bandwidth, accepting support for only four formats and a first-hit network cost,
because reading a few header bytes is enough and must never break rendering.
