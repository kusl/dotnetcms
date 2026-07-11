---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [ui, blazor, architecture]
---

# 0003 — Blazor Server (Interactive Server) as the UI model

## Context and Problem Statement

MyBlog needs an interactive admin UI (post editor with live preview, image
manager, user management) and a fast, SEO-friendly public site, all in .NET,
without adopting a JavaScript SPA framework or an npm toolchain (see ADR-0006).
Which rendering model should the presentation layer use?

## Decision Drivers

- No npm / Node build step; the UI must be pure .NET + a little hand-written JS.
- Good SEO for public pages (server-rendered HTML, meta tags, JSON-LD).
- Rich interactivity for admin pages (live Markdown preview, file upload).
- A single deployable process (no separate API + SPA hosting).

## Considered Options

1. **Blazor WebAssembly** — client-side .NET, but ships a large runtime, weak SEO
   without prerendering, and needs a separate API surface.
2. **MVC / Razor Pages** — great SEO, but interactivity means writing lots of JS.
3. **Blazor Server (Interactive Server)** — server-rendered HTML with
   interactivity delivered over a SignalR circuit.

## Decision Outcome

Chosen option: **Blazor Server** (option 3). `Program.cs` calls
`AddRazorComponents().AddInteractiveServerComponents()` and
`MapRazorComponents<App>().AddInteractiveServerRenderMode()`.

Public pages (`Home`, `PostDetail`, `About`) render on the server for SEO; admin
pages opt into interactivity with `@rendermode InteractiveServer`. `PostDetail`
injects Open Graph, Twitter Card, and JSON-LD metadata via `<HeadContent>`.

### Consequences

- Good: one process, no client build, first-class server rendering for SEO.
- Good: the circuit's websocket is reused by the framework; real-time features
  (ADR-0014) fit naturally.
- Neutral: the app is stateful per connection; scaling out requires sticky
  sessions or a backplane — acceptable given the single-instance SQLite model
  (ADR-0004).
- Bad: interactivity requires a live connection; a dropped circuit degrades the
  admin experience until reconnect.
- Bad: the interactive-vs-static rendering distinction is subtle and caused a real
  authentication bug — see ADR-0013.

## More Information

The theme switcher and share button are the only pieces of hand-written client JS
(`wwwroot/js/site.js`); everything else is Blazor components. An inline script in
`App.razor` sets the theme before first paint to avoid a flash of the wrong theme.

**Y-statement:** In the context of a .NET blog needing both SEO and admin
interactivity without a JS SPA, facing the cost of an npm toolchain and a split
API/SPA deployment, we decided for Blazor Server and neglected Blazor WASM and
plain Razor Pages, to achieve server rendering plus interactivity in one process,
accepting per-connection state and a live-connection requirement, because a single
self-hosted .NET process is the whole point of the project.
