---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [architecture, structure, solid]
---

# 0002 — Clean (Onion) Architecture with a four-assembly split

## Context and Problem Statement

MyBlog is a self-hosted blog engine that must remain testable, must let the
persistence and web frameworks change without rewriting business rules, and must
be understandable by a single maintainer. What overall structure should the
solution take?

## Decision Drivers

- Business/domain logic must be unit-testable without a database or a web host.
- The dependency direction should protect the domain from infrastructure churn
  (EF Core, ASP.NET, SignalR are details).
- The layout should be conventional enough that a newcomer recognizes it instantly.

## Considered Options

1. **Single-project ("all in `MyBlog.Web`")** — fastest to start, hardest to test
   and to keep boundaries honest.
2. **Two projects (Web + Data)** — some separation, but domain rules and data
   access blur together.
3. **Clean / Onion Architecture** with explicit `Core`, `Infrastructure`, `Web`
   assemblies (plus test assemblies).

## Decision Outcome

Chosen option: **Clean Architecture** (option 3), realized as five projects:

- `MyBlog.Core` — domain models, DTOs, service **interfaces**, and pure-logic
  services (`MarkdownService`, `SlugService`, constants). Depends on nothing but
  `Microsoft.Extensions.Logging` abstractions.
- `MyBlog.Infrastructure` — EF Core `BlogDbContext`, repositories, and the
  concrete services that need I/O (auth, password, telemetry exporters, image
  probing). Depends on `Core`.
- `MyBlog.Web` — Blazor Server presentation, minimal-API endpoints, SignalR hub,
  middleware, DI composition root. Depends on `Infrastructure`.
- `MyBlog.Tests` and `MyBlog.E2E` — test assemblies.

The dependency rule points strictly inward (`Web → Infrastructure → Core`). Every
cross-layer collaboration goes through an interface declared in `Core`
(`IPostRepository`, `IAuthService`, `IMarkdownService`, …). This is the
project's realization of the Dependency Inversion Principle and gives the Interface
Segregation Principle for free — the interfaces are small and role-specific.

### Consequences

- Good: `Core` and `Infrastructure` unit/integration tests run with no web host;
  integration tests swap SQLite for an in-memory connection (see ADR-0018).
- Good: swapping the ORM or the web framework is contained to one assembly.
- Neutral: `Core` references the `Microsoft.Extensions.Logging` **abstractions**
  package (used only by `MarkdownService`). Purists dislike any framework
  reference in the domain; we accept it because it is an abstraction, not an
  implementation, and it avoids a bespoke logging interface.
- Bad: more projects and a little ceremony (interfaces + registrations) than a
  single-project app.

## More Information

`MyBlog.Web/Program.cs` is the single composition root; `AddInfrastructure` in
`MyBlog.Infrastructure/ServiceCollectionExtensions.cs` wires the concrete types.
DI lifetimes are chosen deliberately (see ADR-0014 for why `ReaderTrackingService`
is a singleton and ADR-0015 for the exporter registrations).

**Y-statement:** In the context of a maintainable, testable blog engine, facing
the risk of business rules entangling with EF Core and ASP.NET, we decided for
Clean Architecture with inward-pointing dependencies and neglected a single- or
two-project layout, to achieve testability and framework independence, accepting
extra projects and interface ceremony, because the domain must outlive its
infrastructure choices.
