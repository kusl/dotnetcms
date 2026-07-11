# Architecture Decision Records

This directory is the **decision log** for MyBlog (a.k.a. `dotnetcms`). Each file
captures a single Architectural Decision (AD) — a justified design choice that
addresses an architecturally significant requirement — together with its context,
the options considered, and its consequences.

## Why we keep ADRs

Code shows *what* the system does. It rarely explains *why* it does it that way,
or what was deliberately **not** done. MyBlog contains several choices that look
surprising until you know the reasoning (a hand-written Markdown parser, no EF
Core migrations, a seeded `ChangeMe123!` admin password, an SSR login form in an
otherwise-interactive Blazor app). Without a record, that reasoning is re-derived
— or worse, "fixed" — by whoever touches the code next. These ADRs preserve the
hard-won context so future changes are informed rather than accidental.

## Format

We use the [MADR](https://adr.github.io/madr/) template (Markdown Any Decision
Record). Each record also closes with a **Y-statement** — the one-sentence form
from *Sustainable Architectural Decisions* (Zdun et al.):

> In the context of `<use case>`, facing `<concern>`, we decided for `<option>`
> and neglected `<alternatives>`, to achieve `<quality>`, accepting `<downside>`,
> because `<rationale>`.

## Conventions

- **Numbering:** `NNNN-kebab-case-title.md`, monotonically increasing, never reused.
- **Status:** `proposed` | `accepted` | `deprecated` | `superseded by ADR-XXXX`.
- **Immutability:** an accepted ADR is not rewritten when we change our minds. We
  add a *new* ADR that supersedes it and update the `status` line of the old one.
- **Dates** reflect when the decision was effectively made (from the git history),
  not when this document was written. Several foundational decisions were made at
  project inception (2025-12-28) and only written up retroactively on 2026-07-11.

## Deciders

MyBlog is a solo project by **Kushal**, developed with AI assistance (Claude by
Anthropic and Gemini by Google), as disclosed in the root `README.md`. "Deciders"
below reflects that reality.

## Index

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted | 2026-07-11 |
| [0002](0002-clean-architecture-layering.md) | Clean (Onion) Architecture with a four-assembly split | Accepted | 2025-12-28 |
| [0003](0003-blazor-server-ui.md) | Blazor Server (Interactive Server) as the UI model | Accepted | 2025-12-28 |
| [0004](0004-sqlite-single-file-store.md) | SQLite single-file datastore; images stored as BLOBs | Accepted | 2025-12-28 |
| [0005](0005-no-ef-migrations.md) | No EF Core migrations; `EnsureCreated` + idempotent schema updater | Accepted | 2026-01-20 |
| [0006](0006-dependency-and-license-minimalism.md) | Dependency and license minimalism | Accepted | 2025-12-28 |
| [0007](0007-custom-markdown-parser.md) | Hand-written Markdown parser | Accepted | 2025-12-28 |
| [0008](0008-image-dimension-probe.md) | Custom image-dimension probe, cache, and startup warmer | Accepted | 2026-01-16 |
| [0009](0009-progressive-login-rate-limiting.md) | Progressive-delay login rate limiting that never blocks | Accepted | 2025-12-28 |
| [0010](0010-password-hashing.md) | Password hashing via ASP.NET Core Identity `PasswordHasher` | Accepted | 2025-12-28 |
| [0011](0011-cookie-auth-single-admin-role.md) | Cookie authentication with a single `Admin` authorization model | Accepted | 2025-12-28 |
| [0012](0012-seeded-admin-changeme-password.md) | Seeded default admin and the `ChangeMe123!` password | Accepted | 2025-12-28 |
| [0013](0013-ssr-login-form.md) | Authentication via a static SSR form posting to a minimal API | Accepted | 2026-01-26 |
| [0014](0014-signalr-reader-tracking.md) | Real-time reader counts via SignalR and in-memory state | Accepted | 2026-01-16 |
| [0015](0015-opentelemetry-vendor-neutral.md) | OpenTelemetry with vendor-neutral OTLP export to Honeycomb | Accepted | 2026-03-16 |
| [0016](0016-xdg-storage-paths.md) | XDG-compliant storage paths with writability fallback | Accepted | 2025-12-28 |
| [0017](0017-central-package-management.md) | Centralized package management and transitive security pinning | Accepted | 2025-12-28 |
| [0018](0018-testing-strategy.md) | Testing strategy: xUnit v3 / MTP, in-memory SQLite, Playwright | Accepted | 2025-12-28 |
| [0019](0019-iis-webdeploy-ci-cd.md) | CI/CD: IIS WebDeploy from GitHub Actions to two targets | Accepted | 2026-03-16 |
| [0020](0020-csharp-coding-standard.md) | C# coding standard: .NET 10, nullable, warnings-as-errors | Accepted | 2025-12-28 |

## SOLID and "shortcuts" review

ADR [0001](0001-record-architecture-decisions.md) links to a short appendix,
[`solid-review.md`](solid-review.md), that records where the codebase follows
SOLID well, where it deliberately deviates, and the small residual nits that are
knowingly accepted. Read it alongside the ADRs that own each trade-off
(0007 for Markdown/XSS, 0011 for the flat role model, 0014 for single-instance
reader state).
