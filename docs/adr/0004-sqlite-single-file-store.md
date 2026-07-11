---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [data, persistence, storage]
---

# 0004 — SQLite single-file datastore; images stored as BLOBs

## Context and Problem Statement

MyBlog is meant to be trivially self-hostable: clone, run, done. It should require
no external database server and no external object storage, and it should be
backed up by copying a single file. What datastore and what image-storage strategy
achieve this?

## Decision Drivers

- Zero external services — no Postgres/SQL Server to provision, no S3 bucket.
- One-file backup and portability across Windows, Linux, and macOS.
- Simple, transactional consistency between posts, users, and their images.

## Considered Options

1. **SQL Server / PostgreSQL + filesystem or object storage for images** — the
   "scalable" default, but adds operational weight this project explicitly avoids.
2. **SQLite for relational data + local filesystem for images** — one DB file, but
   images become a second thing to back up, secure, and keep in sync.
3. **SQLite for everything, images as `BLOB` columns.**

## Decision Outcome

Chosen option: **SQLite for everything, images as BLOBs** (option 3), via
`Microsoft.EntityFrameworkCore.Sqlite`. The `Images` table stores the bytes in a
`Data BLOB` column; images are served by a public minimal-API endpoint
`GET /api/images/{id:guid}` that returns `Results.File(image.Data, contentType)`.
Uploads are capped at 5 MB and restricted to JPEG/PNG/GIF/WebP (`AppConstants`).

Storing images in the database means a single file (`myblog.db`) is the entire
backup, and post↔image consistency is enforced by foreign keys
(`Image.PostId` → `SET NULL`, `Image.UploadedByUserId` → `RESTRICT`).

### Consequences

- Good: back up or move the whole site by copying one file.
- Good: transactional integrity across all content; no orphaned files.
- Neutral: reads use `AsNoTracking()`; the image endpoint streams the byte array.
- Bad: the DB file grows with image payloads; very large media libraries would be
  better served by object storage. Acceptable for a personal blog.
- Bad: SQLite is effectively single-writer and single-instance. Horizontal
  scale-out is not supported. This is a deliberate boundary and is consistent with
  the in-memory reader-tracking state (ADR-0014) and per-connection Blazor state
  (ADR-0003).

## More Information

The database file location is resolved per-platform (ADR-0016). Schema is created
and evolved without EF migrations (ADR-0005). External images referenced in
Markdown are handled separately by the dimension probe (ADR-0008); only *uploaded*
images live as BLOBs.

**Y-statement:** In the context of a trivially self-hostable blog, facing the
operational cost of external databases and object storage, we decided for a single
SQLite file with images as BLOBs and neglected a client/server DB plus filesystem
media, to achieve one-file backup and transactional consistency, accepting
single-writer/single-instance limits and DB growth from media, because zero
external dependencies is a core project goal.
