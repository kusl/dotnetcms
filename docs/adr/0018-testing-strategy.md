---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [testing, quality, xunit, playwright, containers]
---

# 0018 — Testing strategy: xUnit v3 / MTP, in-memory SQLite, Playwright

## Context and Problem Statement

The project needs fast, reliable automated tests at multiple levels — unit,
integration against a real database engine, and end-to-end through a browser — while
staying inside the dependency budget (ADR-0006, which bans mocking libraries) and
running cleanly on both Linux and Windows and in CI.

## Decision Drivers

- Fast, deterministic unit and integration tests.
- Integration tests that exercise **real SQLite behavior**, not a fake.
- No mocking-framework dependency — hand-written fakes only.
- E2E that runs identically locally and in CI, cross-platform.
- Modern test tooling (xUnit v3 on the Microsoft Testing Platform).

## Considered Options

- **Unit/integration runner:** xUnit v3 on **Microsoft.Testing.Platform** (`dotnet
  run`) vs. the classic VSTest host. Chose MTP for the modern, self-hosted model.
- **Integration data store:** **in-memory SQLite** (`Data Source=:memory:` with an
  explicitly opened connection) vs. the EF Core InMemory provider. Chose real SQLite
  so provider quirks are exercised; the InMemory provider isn't SQLite and hides
  them.
- **Test doubles:** **hand-written fakes** vs. Moq/NSubstitute/FakeItEasy. Chose hand
  fakes, per ADR-0006.
- **E2E:** **Playwright for .NET** in containers vs. Selenium or a hosted service.
  Chose Playwright.

## Decision Outcome

Chosen: **xUnit v3 on MTP** for unit and integration tests, run via `dotnet run`.
Integration tests use **in-memory SQLite** by opening a `:memory:` connection and
keeping it open for the test's lifetime, then instantiating the systems under test
**directly** (no DI container, no mocking library — dependencies are hand-built
fakes). Cancellation flows through `TestContext.Current.CancellationToken` per the
xUnit v3 pattern. String assertions **normalize newlines** first, because
`StringBuilder.AppendLine` emits CRLF on Windows and LF on Linux — without
normalization the same test passes on one OS and fails on the other.

**E2E** uses **Playwright for .NET** with a shared `PlaywrightFixture`
(`ICollectionFixture`, so the browser/context is created once per collection). It
runs in **Podman locally and Docker in CI**. The Dockerfile deliberately orders its
layers so the **browser download is cached before application source is copied** —
editing source doesn't re-download browsers — and sets offline-support flags; CI
infrastructure includes stale-container cleanup.

**Documentation caveat (recorded so it isn't mistaken for missing work):** an early
design note (`playwright.md`) describes an aspirational, MSTest-style "epic" E2E
suite. That elaborate structure was **not** implemented. The actual E2E tests are
simpler **per-page xUnit classes**. The note is a discarded plan, not a spec the
code fails to meet; `solid-review.md` flags the doc/reality gap.

### Consequences

- Good: integration tests catch real SQLite provider behavior, not an in-memory
  stand-in's.
- Good: no mocking dependency; fakes are explicit and readable.
- Good: E2E is reproducible locally and in CI, and the layer ordering keeps image
  builds fast.
- Good: newline normalization makes the suite genuinely cross-platform.
- Neutral: direct instantiation means tests know a bit about construction; accepted
  for clarity and to avoid a container in tests.
- Bad: an out-of-date design note (`playwright.md`) can mislead a reader into
  thinking the suite is incomplete. Mitigation: this ADR and `solid-review.md` state
  plainly that it's aspirational; the note itself is left in place as history.

## More Information

Enforces the runtime decisions of ADR-0004 (SQLite) and the dependency rules of
ADR-0006; the container/CI mechanics connect to ADR-0019.

**Y-statement:** In the context of testing a cross-platform Blazor/SQLite app
without mocking libraries, facing the need for fast unit tests, faithful data-layer
tests, and reproducible browser tests, we decided for xUnit v3 on MTP with in-memory
SQLite, hand-written fakes, and containerized Playwright and neglected the EF
InMemory provider, mocking frameworks, and Selenium, to achieve fast deterministic
tests that exercise real SQLite and run identically everywhere, accepting direct SUT
construction and a stale aspirational E2E design note, because faithful behavior and
zero mocking dependencies matter more than test-time indirection.
