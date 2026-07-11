---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [security, auth, bootstrapping, first-run]
---

# 0012 — Seeded default admin and the `ChangeMe123!` password

## Context and Problem Statement

A fresh deployment has an empty database and therefore no users, yet the whole
point of the app is to log in and write. Something has to create the first admin.
We must balance a **zero-configuration first run** ("clone, run, log in") against
the obvious danger of a well-known default credential. This ADR exists because the
`ChangeMe123!` literal looks like a mistake and is not — it is a deliberate,
last-resort fallback, and this record captures the *why* and the *how* so nobody
"fixes" it into a worse state.

## Decision Drivers

- The app must be usable immediately on first run with **no required configuration**.
- A production operator must be able to set a **real** password without editing code.
- Seeding must be **idempotent and non-destructive** — never overwrite an existing
  admin or clobber a chosen password on restart.
- The insecure path must be **loud and self-documenting** (the password literally
  says "change me").

## Considered Options

1. **Interactive first-run setup wizard** (create the admin through the UI on first
   boot). Better UX story, but a meaningful amount of one-time code and a bootstrap
   auth surface of its own.
2. **Require an admin password via env/config with no fallback** — refuse to start
   otherwise. Safest default, but breaks "clone and run" and complicates local dev,
   containers, and CI.
3. **Seed a fixed admin with a configurable password and a well-known fallback
   literal.** Zero-config by default, secure when configured.

## Decision Outcome

Chosen option: **seed a fixed admin with a configurable password and a well-known
fallback.** `AuthService.EnsureAdminUserAsync` runs at startup (after the schema is
ensured, ADR-0005) and creates the admin **only when `AnyUsersExistAsync()` is
false**. It never touches an existing user, so restarts and redeploys are safe.

The password is resolved by precedence:

1. `MYBLOG_ADMIN_PASSWORD` environment variable, else
2. `Authentication:DefaultAdminPassword` configuration, else
3. the literal **`ChangeMe123!`**.

The seeded account is fixed: username `admin`, display name `Administrator`, email
`admin@localhost`, with the password hashed via ADR-0010 before storage. Real
deployments set the env var (it is wired through the deployment pipeline, ADR-0019),
so the literal is never used in production; it exists so a fresh clone, a local dev
run, and the E2E containers all come up working with no setup step.

### Consequences

- Good: true zero-config first run — nothing to configure to get a working login.
- Good: production stays secure via a single environment variable; no code change,
  no rebuild.
- Good: idempotent and non-destructive — the seeder can't overwrite a chosen
  password, so there's no "restart reset my admin" foot-gun.
- Bad: if an operator ignores every signal and ships with the fallback, the
  credential is public knowledge. This is a deliberately accepted risk: the value
  names itself "ChangeMe", it is documented in the README, and it is gated behind
  ADR-0009's login throttling. The mitigation is operational (set the env var), by
  design.
- Neutral: username/email are hardcoded rather than configurable; adequate for a
  single-operator blog and trivially changeable after first login.

## More Information

This is the canonical example of a **deliberate decision** in this log: the code
looks careless, the reasoning is not. See ADR-0010 (how the password is hashed),
ADR-0011 (what the resulting identity is allowed to do), and ADR-0019 (how the real
password reaches production).

**Y-statement:** In the context of bootstrapping the first admin on an empty
database, facing the tension between zero-config usability and default-credential
risk, we decided for an idempotent seeder with an env/config-overridable password
and a self-naming `ChangeMe123!` fallback and neglected a mandatory-password
start-up gate and a first-run wizard, to achieve "clone and run" locally while
staying secure in production, accepting that an operator who ignores every warning
ships a public credential, because the fallback is loud, documented, throttled, and
overridden by a single environment variable in real deployments.
