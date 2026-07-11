---
status: accepted
date: 2026-01-16
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [realtime, signalr, state, scalability]
---

# 0014 — Real-time reader counts via SignalR and in-memory state

## Context and Problem Statement

The blog shows a live "readers on this post" count that updates as visitors arrive
and leave. This needs a push channel to the browser and a place to hold the current
per-post counts. What transport and what state store, given the single-instance
deployment model (ADR-0004)?

## Decision Drivers

- Push updates to clients without polling.
- Track presence per post accurately as connections open and close.
- Stay dependency-light (ADR-0006) and consistent with Blazor Server (ADR-0003).
- Match the actual deployment: one process, one node.

## Considered Options

1. **SignalR hub + an in-memory concurrent store.** Native to ASP.NET Core, already
   present for Blazor Server, no external moving parts.
2. **SignalR + a Redis backplane / distributed cache.** Enables scale-out and shared
   state, but adds infrastructure the deployment doesn't have.
3. **Client polling of a count endpoint.** Simple, but chatty and not truly live.

## Decision Outcome

Chosen option: **SignalR hub with in-memory state.** `ReaderHub` is the SignalR
endpoint; `ReaderTrackingService` holds the state as `ConcurrentDictionary`s mapping
post slug → current count and connection id → slug, so a disconnect decrements the
right post. The service is registered as a **Singleton** because the state must be
shared across all connections in the process.

On the client, `ReaderBadge.razor` opens **its own `HubConnection`** from within the
Blazor circuit (`WithAutomaticReconnect`, and it implements `IAsyncDisposable` to
tear the connection down), subscribes to count updates, and renders the badge. Using
a dedicated hub connection keeps the presence protocol independent of the render
circuit's lifecycle.

### Consequences

- Good: genuinely live counts with no polling; zero new infrastructure.
- Good: connection-keyed bookkeeping makes leave-events decrement the correct post.
- Bad: state is **per-process and in-memory**, so counts are correct only in a
  single-instance deployment — the same constraint accepted in ADR-0004 and
  ADR-0009. A scale-out would require a backplane (option 2) and moving the
  dictionaries behind a shared store. This limit is deliberate and recorded, not
  overlooked.
- Neutral: counts reset on restart. Acceptable — they are ephemeral presence, not
  durable data.
- Neutral (nits, tracked in `solid-review.md`, not fixed here): `ReaderTrackingService`
  is a `public class` rather than `sealed` like the other services, and
  `ReaderBadge` logs via `Console.WriteLine` instead of `ILogger`. Both are cosmetic
  and left alone to avoid churn (ADR-0001's no-needless-change stance).

## More Information

Builds on ADR-0003 (Blazor Server / SignalR already in the stack) and shares the
single-instance assumption of ADR-0004.

**Y-statement:** In the context of showing live reader counts per post, facing the
choice of transport and state store on a single-node deployment, we decided for a
SignalR hub backed by in-memory concurrent state and neglected a Redis backplane and
client polling, to achieve real-time counts with no added infrastructure, accepting
that the counts are correct only for a single instance and reset on restart, because
the deployment is single-instance and presence data is inherently ephemeral.
