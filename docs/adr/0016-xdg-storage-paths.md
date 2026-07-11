---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [storage, cross-platform, filesystem, xdg]
---

# 0016 — XDG-compliant storage paths with writability fallback

## Context and Problem Statement

The app writes files it owns — the SQLite database (ADR-0004) and, when enabled, log
files (ADR-0015). Where on disk should these live so the app behaves well on Linux
(the primary dev/deploy OS) and Windows (secondary), without dumping files in the
working directory or failing on a read-only or missing location?

## Decision Drivers

- Follow platform conventions rather than hardcoding paths — respect the
  XDG Base Directory spec on Linux.
- Work across Fedora (primary) and Windows (secondary).
- Fail gracefully: if the preferred directory isn't writable, fall back rather than
  crash.
- Keep runtime data out of the deploy/working directory.

## Considered Options

1. **Resolve an XDG-compliant data directory, with a writability check and
   fallback.** Convention-driven and defensive.
2. **Hardcode a relative path** next to the binary. Simple, but pollutes the deploy
   dir and breaks under read-only or per-user deployment layouts.
3. **Require the path via configuration** with no default. Explicit, but breaks
   zero-config first run (cf. ADR-0012).

## Decision Outcome

Chosen option: **XDG-compliant resolution with a writability fallback.** Dedicated
resolvers compute the storage location from the XDG data directory (honoring the
relevant environment variables) with sensible per-OS defaults, **verify the target
is writable**, and fall back to an alternative if not — so the app comes up with a
usable location instead of throwing on startup.

There are two such resolvers — `DatabasePathResolver` and `TelemetryPathResolver` —
because the database and the telemetry log files are resolved independently. They
currently contain **near-identical XDG logic**. This duplication is a known, minor
**DRY** violation: it is called out here (and in `solid-review.md`) as a candidate
for extracting a shared path helper, but is **left as-is** for now under ADR-0001's
"don't make needless changes that risk regressions" stance — the duplication is
small, correct, and covered by tests.

### Consequences

- Good: files land in conventional, per-user locations on Linux and appropriate
  defaults on Windows; nothing is written to the deploy directory.
- Good: a non-writable or missing preferred directory degrades to a fallback instead
  of a startup crash.
- Neutral: the XDG logic is duplicated across two resolvers — a small accepted DRY
  debt with a clear, low-risk refactor available when it's worth touching.
- Neutral: paths are derived, not configured; adequate here and consistent with the
  zero-config posture of ADR-0012.

## More Information

Consumed by ADR-0004 (database location) and ADR-0015 (log-file location).

**Y-statement:** In the context of choosing where the app writes its database and
logs across Linux and Windows, facing the need to respect platform conventions
without crashing on unwritable locations, we decided for XDG-compliant resolvers
with a writability fallback and neglected hardcoded relative paths and mandatory
path configuration, to achieve conventional per-user placement that degrades
gracefully, accepting a small DRY duplication between the two resolvers, because
convention-plus-fallback is robust and the duplication is minor and test-covered.
