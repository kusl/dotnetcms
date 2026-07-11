---
status: accepted
date: 2026-01-26
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [auth, blazor, rendering, reliability]
---

# 0013 — Authentication via a static SSR form posting to a minimal API

## Context and Problem Statement

`SignInAsync` writes the auth cookie onto the HTTP response, which requires a real,
uncommitted HTTP response — something an established Blazor Server (SignalR) circuit
does not have. Setting a cookie from inside an interactive circuit is therefore
fragile. The admin area is otherwise built as interactive Blazor. How should the
one page that establishes the session — login — actually submit credentials?

## Decision Drivers

- The sign-in must run where it can legitimately set a response cookie.
- Login must be **reliable on mobile browsers**, not merely "usually fine."
- Antiforgery protection must hold without racing the framework's enhanced
  navigation.
- Keep the rest of the admin UI interactive (ADR-0003) without letting login's
  needs distort it.

## Considered Options

1. **Static SSR form → minimal-API endpoint.** `Login.razor` renders a plain static
   form that POSTs to `/account/login`; the endpoint validates and calls
   `SignInAsync` on a genuine HTTP response, then redirects.
2. **`@rendermode InteractiveServer` login component** handling submit inside the
   circuit. Consistent with the rest of admin, but pushes cookie-writing into the
   circuit — exactly the fragile path.
3. **Enhanced-navigation form** (Blazor's default enhanced form posting). Convenient,
   but its client-side interception is what produced the antiforgery race below.

## Decision Outcome

Chosen option: **static SSR form posting to a minimal API.** `Login.razor` is a
deliberately static server-rendered page: it opts **out** of enhanced navigation
with `data-enhance="false"`, embeds `<AntiforgeryToken />`, and uses plain
`<input name="...">` fields. The form POSTs to the minimal-API endpoint
`/account/login`, which validates the antiforgery token, verifies the credential
(ADR-0010), calls `SignInAsync` on the real response, and redirects. Logout is the
symmetric CSRF-protected POST (ADR-0011). The rest of the admin area remains
`@rendermode InteractiveServer`.

**Why this specific shape — the journey that produced it:** an earlier version used
an enhanced Blazor form and saw **intermittent HTTP 400s on mobile**, caused by the
antiforgery token racing Blazor's enhanced navigation during submit. We first tried
disabling enhancement (`data-enhance="false"`), then briefly switched the whole page
to `@rendermode InteractiveServer` (see the interactive-login experiment in chat),
which moved cookie-writing into the circuit and did not resolve it cleanly. We
**reverted** to the static-SSR-form → minimal-API design, which sidesteps both
problems at once: the cookie is written on a normal response, and there is no
enhanced-nav interception to race the token. This ADR records the endpoint as the
settled answer so the interactive detour is not attempted again. (Login form
settled 2026-01-26; the revert away from the interactive experiment landed in the
March auth changes.)

### Consequences

- Good: sign-in happens exactly where a response cookie can be set — no
  circuit-cookie fragility.
- Good: login is reliable across mobile and desktop; the antiforgery race is gone.
- Good: login's constraints are quarantined to one static page + one endpoint,
  leaving the rest of admin interactive.
- Neutral: the app deliberately **mixes render modes** — static SSR for login,
  interactive for admin. That is intentional and correct here, but it is a sharp
  edge: mixing the two carelessly is a known source of Blazor breakage, so the
  boundary is explicit.
- Bad: login is a small amount of "different-shaped" code (a static page plus a
  minimal-API handler) rather than one uniform component style. Accepted as the
  cost of reliability.

## More Information

Depends on ADR-0003 (Blazor Server) and ADR-0010 (credential check); the resulting
cookie/role model is ADR-0011.

**Y-statement:** In the context of establishing the auth cookie for a
mostly-interactive Blazor admin, facing the fact that circuits can't reliably set
response cookies and that enhanced-nav raced the antiforgery token on mobile, we
decided for a static SSR form posting to a minimal-API endpoint and neglected an
interactive-circuit login and enhanced forms, to achieve reliable cross-device
sign-in with intact CSRF protection, accepting a deliberate render-mode split and a
little non-uniform code, because the cookie must be written on a real response and
the static form removes the race entirely.
