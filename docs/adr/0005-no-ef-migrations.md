---
status: accepted
date: 2026-01-20
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [data, persistence, deployment, data-safety]
---

# 0005 — No EF Core migrations; `EnsureCreated` + idempotent schema updater

## Context and Problem Statement

The schema must be created on a fresh install and evolved on existing, live
databases **without losing data**. The project was bootstrapped from a generation
script and never carried an EF Core migrations history. A build once shipped
`Database.MigrateAsync()`, which failed at runtime because there were no migration
files — the database was left empty and the `Users` query threw. How should schema
be created and evolved?

## Decision Drivers

- A fresh clone must produce a working schema with no extra steps.
- Existing production databases (e.g. the live demo) must gain new tables on deploy
  **without data loss** — data safety on live deployments is a hard constraint.
- Keep the model simple; avoid maintaining a migrations folder for a small schema.

## Considered Options

1. **Adopt EF Core migrations** (`Add-Migration` / `MigrateAsync`) — the standard
   approach, but requires generating and committing migrations, and retrofitting an
   initial migration onto databases that were created by `EnsureCreated`.
2. **`EnsureCreated` only** — creates everything on a fresh DB but does *nothing*
   to an existing DB, so new tables never appear on upgrade.
3. **`EnsureCreated` + a hand-written, idempotent `DatabaseSchemaUpdater`.**

## Decision Outcome

Chosen option: **`EnsureCreated` + idempotent updater** (option 3). Startup runs,
in order: `Database.EnsureCreatedAsync()`, then
`DatabaseSchemaUpdater.ApplyUpdatesAsync(context)`, then
`AuthService.EnsureAdminUserAsync()`.

`DatabaseSchemaUpdater` inspects `sqlite_master` (via a parameterized query) and
issues `CREATE TABLE IF NOT EXISTS` for tables that post-date the original schema
(currently `ImageDimensionCache`). It is safe to run repeatedly and only ever
*adds* objects — it never drops or alters, so it cannot destroy data.

### Consequences

- Good: fresh installs and upgrades both work with zero manual steps and no data
  loss; the updater is covered by `DatabaseSchemaUpdaterTests` (fresh DB, missing
  table, idempotency).
- Good: no migrations folder to maintain for a small, additive schema.
- Bad: **no support for column renames, drops, or type changes.** Any such change
  must be hand-written as bespoke, data-preserving SQL in the updater. This is the
  main cost and is accepted knowingly.
- Bad: the schema in `OnModelCreating` and the `CREATE TABLE` SQL in the updater
  must be kept in agreement by hand.

## More Information

If the schema ever needs destructive evolution, the pragmatic path is to introduce
EF migrations *from that point forward* with a baseline migration matching the
current `EnsureCreated` schema — a follow-up ADR would supersede this one.

**Y-statement:** In the context of a small SQLite schema that must upgrade live
databases without data loss, facing a failed `MigrateAsync` with no migration
history, we decided for `EnsureCreated` plus an idempotent additive schema updater
and neglected full EF migrations, to achieve zero-touch, data-safe schema
evolution, accepting that column renames/drops require hand-written SQL, because
the schema is small and additive and data safety outweighs migration flexibility.
