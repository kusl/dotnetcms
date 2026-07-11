# SOLID review and accepted shortcuts

This appendix supports [ADR-0001](0001-record-architecture-decisions.md). It records
where the codebase follows SOLID well, where it **deliberately** deviates (each such
deviation is owned by a numbered ADR), and the small residual nits that are knowingly
accepted. It is a review artifact, not a decision itself.

**Ground rule for this review:** the build and the full unit, integration, and E2E
suites currently pass. Per ADR-0001's "no needless changes / no regressions" stance,
nothing below was *changed* as part of writing these ADRs. Genuine improvements are
listed as recommendations at the end, prioritized, for a future intentional change.

## SOLID adherence

**Single Responsibility (SRP)** — Generally good. Responsibilities are split into
focused services: `MarkdownService` (parsing), `SlugService` (slugs),
`PasswordService` (hashing), `ImageDimensionService` (image probing),
`ReaderTrackingService` (presence), `AuthService` (auth + admin seeding). Repositories
handle persistence; Blazor components handle presentation. The one service doing
slightly more than its name is `AuthService`, which also seeds the default admin —
reasonable, since seeding *is* an auth concern (ADR-0012).

**Open/Closed (OCP)** — Reasonable for the size. New behavior is generally added
behind the Core interfaces without editing consumers. The custom telemetry exporters
extend `BaseExporter<LogRecord>` rather than modifying pipeline internals (ADR-0015).

**Liskov Substitution (LSP)** — Not stressed. Inheritance is shallow and used mainly
for framework base classes (exporters, `DbContext`); the interface implementations are
straightforward and substitutable. No LSP violations observed.

**Interface Segregation (ISP)** — Well followed. Core interfaces are small and
single-purpose (`IPasswordService`, `ISlugService`, `IMarkdownService`,
`IImageDimensionService`, the repository interfaces). Consumers depend only on the
narrow surface they use.

**Dependency Inversion (DIP)** — Well followed and the backbone of the architecture
(ADR-0002). `MyBlog.Core` defines interfaces and depends only on logging abstractions;
`MyBlog.Infrastructure` provides implementations; `MyBlog.Web` composes them in
`Program.cs`. High-level policy does not depend on low-level detail.

**Other quality points observed:** read queries use `AsNoTracking`; services are
`sealed` by default; DI lifetimes are chosen deliberately (singletons for stateless or
shared-state services, scoped for request-bound ones); exceptions in the custom
exporters are isolated so telemetry can't crash requests.

## Deliberate deviations (each owned by an ADR)

These are conscious trade-offs, not oversights. They are documented so they are not
"fixed" into something worse.

- **No URL scheme filtering in the Markdown parser** → possible `javascript:` URI XSS
  in links/images. Tolerable **only** because authoring is admin-only (single-operator
  trust model). Owned by [ADR-0007](0007-custom-markdown-parser.md). Recommended future
  hardening: allow-list URL schemes.
- **Single `Admin` role** — every authenticated user is a full admin; no author/editor
  tiers and no least-privilege separation between operators. Owned by
  [ADR-0011](0011-cookie-auth-single-admin-role.md).
- **Seeded `ChangeMe123!` fallback password** — a deliberate zero-config bootstrap with
  a self-naming, overridable fallback. Owned by
  [ADR-0012](0012-seeded-admin-changeme-password.md).
- **No EF Core migrations** — `EnsureCreated` + an idempotent schema updater; supports
  additive changes but not column rename/drop. Owned by
  [ADR-0005](0005-no-ef-migrations.md).
- **In-memory reader state and rate-limit state** — correct only for a single instance;
  a scale-out needs shared state. Owned by
  [ADR-0014](0014-signalr-reader-tracking.md) and
  [ADR-0009](0009-progressive-login-rate-limiting.md).

## Accepted nits (known, low-risk, intentionally not changed)

| Nit | Where | Why it's left | Owning ADR |
|-----|-------|---------------|------------|
| `ReaderTrackingService` is `public class`, not `sealed` | Infrastructure | Cosmetic; other services are sealed. Sealing is a one-word, zero-risk change worth doing next time the file is touched | 0014, 0020 |
| `Console.WriteLine` instead of `ILogger` | `ThemeSwitcher.SelectThemeAsync`, `ReaderBadge` | Cosmetic logging inconsistency; the rest of the app uses `ILogger` | 0014 |
| User id read via `IHttpContextAccessor` vs `AuthenticationStateProvider` | `ChangePassword` vs `ImageManager`/`PostEditor`/`UserList` | Both are correct for their render model; the `AuthenticationStateProvider` form is the idiomatic one to converge on | 0011, 0013 |
| README delay formula off by one | root `README.md` prose (`2^n` vs code `2^(n-1)`) | The README **table** (1,2,4,8,16,30) is correct; only the one-line prose formula is off. Erratum, not a code bug | 0009 |
| Stale duplicate workflow | `src/.github/workflows/build-deploy.yml` | GitHub only reads the repo-root `.github`; the file is dead and harmless. Removal recommended | 0019 |
| Near-identical XDG logic duplicated | `DatabasePathResolver`, `TelemetryPathResolver` | Small DRY debt; correct and test-covered. Extract a shared helper when convenient | 0016 |
| Parameterless ctor "for tests" | `PasswordService` | Justified — the service is stateless and has no dependencies; harmless testability affordance | 0010 |
| Aspirational E2E design note vs reality | `playwright.md` describes an epic MSTest-style suite that was not built | The real tests are simpler per-page xUnit classes; the note is discarded history, not an unmet spec | 0018 |

## Recommendations (prioritized, for a future intentional change)

1. **Allow-list URL schemes in the Markdown parser** (closes the `javascript:` XSS
   even under the admin-only trust model). Highest security value. — ADR-0007
2. **Fix the README delay-formula prose** so it reads `2^(n-1)` and matches the code
   and the table. Trivial, removes a real point of confusion. — ADR-0009
3. **Delete the stale `src/.github/workflows/build-deploy.yml`.** Zero-risk cleanup;
   prevents a future reader editing the dead file. — ADR-0019
4. **Seal `ReaderTrackingService`** and switch the two `Console.WriteLine` call sites
   to `ILogger`. Cosmetic consistency. — ADR-0014
5. **Unify user-id access on `AuthenticationStateProvider`** in `ChangePassword`.
   Consistency; low value, do it only when that code is next touched. — ADR-0011
6. **Extract a shared XDG path helper** used by both resolvers. Removes the DRY debt
   once it's worth the churn. — ADR-0016

None of these is required for correctness today; each is a deliberate, reviewable
change to be made on its own merits rather than bundled into this documentation pass.
