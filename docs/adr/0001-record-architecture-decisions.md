---
status: accepted
date: 2026-07-11
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [process, documentation]
---

# 0001 — Record architecture decisions

## Context and Problem Statement

MyBlog began life as a single generated shell script and has since evolved through
many small, interactive sessions: bug fixes, feature additions, and re-designs.
Several of its choices are non-obvious and repeatedly get questioned or
accidentally reverted — for example the hand-written Markdown parser, the absence
of EF Core migrations, the seeded `ChangeMe123!` password, and the static-SSR
login form inside an otherwise-interactive Blazor application. The *reasons* for
these choices live only in chat transcripts and one large `README.md`; they are
not attached to the decisions themselves.

How do we preserve the rationale and trade-offs of architecturally significant
decisions so that future work is informed rather than a re-litigation of settled
questions?

## Decision Drivers

- New contributors (and future-me, and AI assistants working from a code dump)
  need the *why*, not just the *what*.
- Some decisions are deliberate deviations from common practice and must not be
  "helpfully corrected" without understanding the cost.
- The record must be low-ceremony: this is a solo project and heavyweight process
  would simply not be maintained.

## Considered Options

1. **Do nothing** — rely on the `README.md` and commit messages.
2. **A single "design decisions" section in the README.**
3. **A dedicated ADR log** using a lightweight, industry-standard template.

## Decision Outcome

Chosen option: **a dedicated ADR log** (option 3), stored under `docs/adr/`, using
the [MADR](https://adr.github.io/madr/) template with a closing Y-statement.

MADR was chosen over the barer Nygard template because MADR's *considered options*
and *consequences* sections are exactly the parts that were missing — the README
already records *what* the system does; the gap is the reasoning and the roads not
taken. The Y-statement adds a one-line summary that is easy to scan.

The README stays authoritative for *how to operate* the system; ADRs become
authoritative for *why the system is shaped the way it is*.

### Consequences

- Good: rationale is versioned next to the code and reviewed with it.
- Good: an accepted ADR is immutable, so history is never quietly rewritten — a
  reversal is a new, superseding ADR, giving an honest audit trail.
- Neutral: writing an ADR is a small tax on each significant decision.
- Bad: the log can drift if decisions are made without recording them; mitigated
  by keeping records short and only for *architecturally significant* choices.

## More Information

The first batch (ADR-0002 … ADR-0020) was written retroactively on 2026-07-11 to
capture existing decisions, so their `date` fields reflect when each decision was
effectively made in the git history, not the authoring date. A companion
`solid-review.md` records SOLID adherence and knowingly-accepted nits.

**Y-statement:** In the context of a long-lived, iteratively-built solo project,
facing the loss of decision rationale across many sessions, we decided for a
MADR-format ADR log and neglected README-only notes, to achieve durable and
reviewable architectural knowledge, accepting a small per-decision writing tax,
because informed change is cheaper than re-derived (or reverted) change.
