---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [security, auth, authorization, simplicity]
---

# 0011 — Cookie authentication with a single `Admin` authorization model

## Context and Problem Statement

The public site is anonymous; a small set of management pages (post editor, image
manager, user list, change password) must be restricted. We need a session
mechanism for the browser and an authorization model for the protected pages. How
rich should that model be for a single-author blog?

## Decision Drivers

- Server-rendered Blazor circuits need a session the framework understands.
- Authorization should be simple to reason about and hard to get subtly wrong.
- Match the actual usage: today there is one operator, not a newsroom.
- Avoid speculative role/claim machinery we don't need (YAGNI, and ADR-0006's
  minimalism).

## Considered Options

1. **Cookie authentication + a single `Admin` role.** Every authenticated user is a
   full administrator; protected pages simply require authentication in that role.
2. **Cookie auth + a granular role/permission model** (Author, Editor, Admin;
   per-action policies). Flexible, but pure overhead for one operator.
3. **Token/JWT auth.** Suited to APIs and SPAs; awkward and unnecessary for a
   server-rendered, same-origin Blazor app.

## Decision Outcome

Chosen option: **cookie authentication with a single `Admin` role.**
`AddAuthentication` + `AddCookie` configure the scheme; the login endpoint
(ADR-0013) always issues a principal carrying `AppConstants.AdminRole`, and every
protected page/component authorizes on that one role. There is intentionally **no
author/editor tier** — being authenticated *is* being an admin.

Supporting details:
- The login/logout cookie is same-site and the logout is a **CSRF-protected POST
  form**, not a bare `GET` link, so a cross-site request can't silently sign a user
  out.
- Roles and related magic strings live in `AppConstants` rather than being
  scattered as literals.
- User identity for the change-password flow is read via `IHttpContextAccessor`,
  whereas the interactive admin components read it via `AuthenticationStateProvider`.
  Both are correct for their render model; the inconsistency is noted in
  `solid-review.md` as a candidate for unification, not a bug.

### Consequences

- Good: trivially auditable authorization — one role, one decision, no policy
  matrix to misconfigure.
- Good: no dependency on an identity server or token infrastructure.
- Neutral: this is a deliberate simplification, not a limitation we're unaware of.
  Adding real roles later means introducing tiers at the seam where the login
  currently hard-assigns `Admin`; the interfaces don't fight it.
- Bad: there is no least-privilege separation between operators — anyone who can log
  in can do everything, including managing other users. Accepted under the
  single-operator trust model that also underpins ADR-0007 and ADR-0012.

## More Information

The credential check behind this cookie is ADR-0010; the form-and-endpoint
mechanics of issuing the cookie are ADR-0013.

**Y-statement:** In the context of protecting the management pages of a
single-author blog, facing the choice between a granular role model and a flat one,
we decided for cookie auth with a single `Admin` role and neglected multi-tier
roles and JWT/token auth, to achieve an authorization model that is trivial to
audit, accepting the absence of least-privilege separation between operators,
because there is one operator and simplicity here removes a whole class of
misconfiguration.
