---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [coding-standard, build, dotnet, style]
---

# 0020 — C# coding standard: .NET 10, nullable, warnings-as-errors

## Context and Problem Statement

A codebase drifts in style and safety unless the standard is enforced by the build
rather than by memory or review discipline. We want modern C#, strong compile-time
null safety, and no tolerance for warnings — but with pragmatism where a modern
feature genuinely doesn't fit. What is the standard, and how is it enforced?

## Decision Drivers

- Modern runtime and language (.NET 10) to use current features.
- Compile-time null-safety, not runtime surprises.
- Warnings must not accumulate — treat them as errors.
- Enforce style in the build so it's not a review chore.
- Concise, current idioms (primary constructors, file-scoped namespaces, source
  generators) — applied with judgment, not dogma.

## Considered Options

1. **Enforce a modern standard in the build** — nullable enable, warnings-as-errors,
   `EnforceCodeStyleInBuild`, primary constructors and file-scoped namespaces by
   default. Machine-enforced consistency.
2. **Conventions by documentation/review only.** Zero build friction, but drifts and
   leans on reviewer vigilance.
3. **A heavier analyzer stack** (e.g. StyleCop plus many rule packages). More rules,
   but more dependency and configuration than this project wants (ADR-0006).

## Decision Outcome

Chosen option: **enforce a modern standard in the build.** Targeting **.NET 10**,
solution-wide settings turn on **nullable reference types**, **treat warnings as
errors**, and set **`EnforceCodeStyleInBuild`** so style violations fail the build.
The solution uses the new **`.slnx`** XML solution format.

Idioms:
- **File-scoped namespaces** everywhere.
- **Primary constructors** as the default for dependency injection — **mixed with
  explicit constructors where an attribute or clarity requires it.** The clearest
  example is the rate-limit middleware (ADR-0009), which needs an explicit
  `[ActivatorUtilitiesConstructor]` to select the DI constructor; forcing a primary
  constructor there would be worse, so an explicit one is used deliberately.
- **`[GeneratedRegex]`** source generation for regexes (compile-time, allocation-
  friendly), consistent with the hand-written Markdown parser's needs (ADR-0007).
- **`sealed`** classes by default for services (a small number of intentional
  exceptions are noted in `solid-review.md`, e.g. `ReaderTrackingService`).

This standard is why ADR-0001's "no needless changes" is easy to honor: the build
already guarantees consistency, so most drift never lands, and cosmetic edits (a
stray `Console.WriteLine`, an unsealed class) are recorded as accepted nits rather
than churned.

### Consequences

- Good: consistency and null-safety are guaranteed by the compiler, not by
  reviewers.
- Good: warnings can't pile up — a warning is a failed build.
- Good: modern, concise code (primary constructors, file-scoped namespaces, source-
  generated regex) without a heavy analyzer dependency.
- Neutral: warnings-as-errors means a new analyzer warning can block a build on a
  framework/SDK bump. Accepted — it surfaces issues immediately rather than letting
  them rot, and the fix is explicit (address it, or scope-suppress with a documented
  reason, never a blanket suppression).
- Neutral: primary constructors are the default but not universal; the exceptions
  are principled and documented, not accidental.

## More Information

Interacts with ADR-0006 (few dependencies → no heavy analyzer stack), ADR-0009 (the
explicit-constructor exception), and ADR-0017 (central versions, also build-enforced).

**Y-statement:** In the context of keeping a solo codebase consistent and null-safe,
facing style drift and accumulating warnings, we decided for a build-enforced modern
standard — .NET 10, nullable, warnings-as-errors, `EnforceCodeStyleInBuild`, primary
constructors and file-scoped namespaces — and neglected review-only conventions and
a heavy analyzer stack, to achieve compiler-guaranteed consistency and safety with
minimal tooling, accepting that an SDK bump can surface a build-blocking warning and
that a few constructors must stay explicit, because machine enforcement beats
discipline and the modern idioms are applied with judgment rather than dogma.
