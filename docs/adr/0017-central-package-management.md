---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [build, dependencies, security, supply-chain]
---

# 0017 — Centralized package management and transitive security pinning

## Context and Problem Statement

The solution has multiple projects (Core, Infrastructure, Web, Tests, E2E) that must
agree on package versions. Divergent versions across projects cause subtle runtime
and restore problems, and a vulnerable **transitive** dependency can't be fixed by
editing a direct reference. How do we manage versions in one place and force-upgrade
a bad transitive package?

## Decision Drivers

- One source of truth for every package version across all projects.
- Ability to override a **transitive** dependency's version for security fixes.
- Consistency with the "no needless drift" ethos and warnings-as-errors build
  (ADR-0020).
- Group and label related versions so bumps are deliberate.

## Considered Options

1. **Central Package Management (CPM)** via `Directory.Packages.props`, plus
   `CentralPackageTransitivePinningEnabled` to pin transitive versions.
2. **Per-project `<PackageReference Version="...">`.** Familiar, but versions drift
   and transitive pinning is awkward.
3. **A shared MSBuild props import of version properties only** (no CPM). Partial
   centralization without CPM's transitive-pinning guarantees.

## Decision Outcome

Chosen option: **Central Package Management.** All versions live in
`Directory.Packages.props` as `<PackageVersion>` entries (grouped and labeled with
MSBuild property variables so related packages move together); individual project
files carry **no version attributes** on their `<PackageReference>` elements.
`CentralPackageTransitivePinningEnabled` is set to **true**, which lets the central
file dictate the resolved version of **transitive** dependencies, not just direct
ones.

That transitive-pinning capability is not theoretical — it is how a real
vulnerability was remediated:

> **Amendment (2026-07-11):** to remediate **CVE-2025-6965** in SQLitePCLRaw, the
> central file pins the SQLitePCLRaw **bundle to 3.0.3** and selects the
> **SourceGear.sqlite3** bundle. Because the vulnerable package arrived
> transitively (via the EF Core SQLite provider, ADR-0004), transitive pinning was
> the mechanism that forced the fixed version across the whole solution from one
> edit. The rest of the log is dated to when each decision was made; this line
> records a later security bump to the same policy.

### Consequences

- Good: a single edit sets a version everywhere; no cross-project drift.
- Good: vulnerable transitive packages can be force-upgraded centrally — proven by
  the CVE-2025-6965 fix.
- Good: grouping/labeling makes version bumps intentional and reviewable.
- Neutral: transitive pinning means the central file must be watched — an
  intentional override could mask an upstream's own newer fix. Accepted; the file is
  small and reviewed on change.

## More Information

Directly enables the security posture; interacts with ADR-0004 (the SQLite provider
that pulled the vulnerable transitive package) and ADR-0020 (warnings-as-errors).

**Y-statement:** In the context of managing package versions across a multi-project
solution, facing version drift and the need to fix vulnerable transitive
dependencies, we decided for Central Package Management with transitive pinning
enabled and neglected per-project version attributes and a props-only partial
scheme, to achieve one source of truth and the ability to force-upgrade transitive
packages, accepting that the central file must be actively maintained, because
supply-chain fixes like CVE-2025-6965 require exactly this one-edit transitive
override.
