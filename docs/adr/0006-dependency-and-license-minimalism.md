---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [dependencies, licensing, supply-chain, philosophy]
---

# 0006 — Dependency and license minimalism

## Context and Problem Statement

MyBlog is a learning-oriented, self-hosted project that the maintainer wants to
understand end-to-end and be able to reason about legally and from a supply-chain
perspective. Modern .NET web apps often pull in dozens of transitive third-party
packages (UI kits, mediators, mappers, mocking libraries, CSS/JS frameworks) with
mixed licenses. What is the project's policy on third-party dependencies?

## Decision Drivers

- **Supply-chain surface:** every added package is code to trust, audit, and keep
  patched.
- **Licensing:** the project should depend only on permissively licensed code
  (MIT / Apache-2.0 / BSD) to avoid copyleft or unclear obligations.
- **Understandability:** the maintainer wants to be able to read and own the whole
  stack rather than treat large frameworks as magic.
- **Longevity:** fewer dependencies means fewer forced upgrades and less churn.

## Considered Options

1. **Use the ecosystem freely** — Markdig for Markdown, a CSS framework, a
   component library, Moq/FluentAssertions for tests, MediatR/AutoMapper for
   plumbing, Polly for resilience. Fastest to build.
2. **Curated allow-list of permissive, first-party-ish packages only**, writing
   everything else by hand.

## Decision Outcome

Chosen option: **a curated allow-list** (option 2). The project depends
essentially only on Microsoft/.NET packages and OpenTelemetry, all under
permissive licenses. The following are **deliberately banned**, with the noted
substitution:

- **Markdig / any Markdown library** → hand-written parser (ADR-0007).
- **Bootstrap / Tailwind / any CSS framework** → hand-written CSS with variables
  and six themes (`wwwroot/css/site.css`).
- **npm / Node / any JS framework** → a little vanilla JS (`wwwroot/js/site.js`).
- **Moq / NSubstitute / FakeItEasy / FluentAssertions** → xUnit assertions +
  hand-written fakes (ADR-0018).
- **MediatR / AutoMapper / Mapster / MassTransit** → explicit method calls and
  hand-written DTO mapping.
- **Polly** → the small amount of retry logic needed is written by hand
  (e.g. the WebDeploy retry flags in CI).
- **Any UI component library** (Radzen, MudBlazor, …) and **any package whose
  license is not MIT / Apache-2.0 / BSD.**

CI also avoids third-party GitHub Actions for deployment, using primitive actions
plus hand-written PowerShell instead (ADR-0019).

### Consequences

- Good: a small, auditable dependency graph of permissively licensed code; the
  maintainer understands the whole stack.
- Good: far less forced-upgrade churn and a smaller attack surface.
- Bad: more code to write and maintain (a Markdown parser, an image-header parser,
  test doubles, CSS). Each such component gets its own ADR documenting its limits.
- Bad: hand-rolled components cover a *subset* of what a mature library would; e.g.
  the Markdown parser supports common syntax only (ADR-0007).
- Neutral: this is a values-driven constraint, not a performance or correctness
  one. It is a standing rule for the project, not a per-feature judgement.

## More Information

Central Package Management (ADR-0017) enforces the allow-list from one file and
pins transitive packages, which is how the SQLite CVE was remediated without a
direct dependency on the vulnerable package.

**Y-statement:** In the context of a self-hosted project the maintainer wants to
fully own, facing supply-chain and licensing risk from large dependency graphs, we
decided for a curated permissive-license allow-list and hand-written substitutes
and neglected free use of the ecosystem, to achieve auditability, legal clarity,
and low churn, accepting that we must build and maintain more ourselves, because
understanding and owning the stack is an explicit goal of the project.
