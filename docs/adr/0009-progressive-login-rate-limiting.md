---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [security, auth, rate-limiting, availability]
---

# 0009 — Progressive-delay login rate limiting that never blocks

## Context and Problem Statement

The login endpoint needs brute-force protection. Classic account lockout (disable
after N failures) is itself a denial-of-service vector: an attacker who knows a
username can lock the legitimate owner out. How do we slow brute force without ever
denying a legitimate user access to their own account?

## Decision Drivers

- Make online password guessing expensive.
- **Never** fully block or lock out a user — availability of one's own account is
  non-negotiable.
- The mechanism must be unit-testable without real wall-clock delays.
- Bound memory so spoofed source IPs can't exhaust it.

## Considered Options

1. **Account lockout after N failures** — standard, but a self-inflicted DoS.
2. **Return HTTP 429 after N attempts** — blocks the attacker but also the victim.
3. **Progressive per-IP delay with no hard block** — every request eventually
   succeeds; failures just get slower.

## Decision Outcome

Chosen option: **progressive per-IP delay, never blocking**
(`MyBlog.Web/Middleware/LoginRateLimitMiddleware.cs`). Only `POST /account/login`
is affected. Per client IP, within a 15-minute window, the first 5 attempts are
instant; after that the delay is exponential — `2^(n-1)` seconds — capped at 30 s
(≈ 1, 2, 4, 8, 16, 30…). The request **always** proceeds after the delay; the
middleware never returns 429/403.

Key design details:
- The delay function is **injectable** (`Func<TimeSpan,CancellationToken,Task>`),
  so unit tests inject a no-op and run in milliseconds; the DI constructor
  (`[ActivatorUtilitiesConstructor]`) uses the real `Task.Delay` and is **disabled
  in Development** so local iteration isn't slowed.
- State is a static `ConcurrentDictionary<string,(int,DateTime)>` with a hard cap
  of `MaxTrackedIps = 10,000`; when exceeded, expired entries are purged and, if
  still full, new IPs simply aren't tracked (they bypass limiting — safe, since the
  threat is guessing existing accounts, and legitimate users are already tracked).
- Client IP comes from `Connection.RemoteIpAddress` and the middleware
  **deliberately does not parse `X-Forwarded-For`** — behind a proxy, configure
  ASP.NET Core `ForwardedHeaders` so the framework sets it from trusted proxies
  only, avoiding spoofable header trust.

### Consequences

- Good: brute force is throttled to a crawl; a legitimate user with the right
  password always gets in (verified by tests up to 1000 consecutive failures).
- Good: fast, deterministic tests via the injected delay; realistic timing tests
  live in the Playwright suite.
- Neutral: rate-limit state is per-instance and in-memory — consistent with the
  single-instance deployment model (ADR-0004); a scale-out would need shared state.
- Bad: delays are per-IP, so a botnet spreading guesses across many IPs sees each
  IP throttled independently. Accepted: hashing + progressive delay still make bulk
  guessing costly, and the app is not a high-value target.

## More Information

The README's inline delay snippet reads `Math.Pow(2, delayMultiplier)`; the code is
`Math.Pow(2, delayMultiplier - 1)`. The **table** in the README (1, 2, 4, 8, 16,
30) matches the code; only the one-line formula in the prose is off by one. Noted
here as an erratum rather than rewriting the 64 KB README.

**Y-statement:** In the context of protecting login from brute force, facing the
self-DoS of account lockout, we decided for a progressive per-IP delay that never
blocks and neglected lockout and hard 429s, to achieve brute-force resistance
without ever denying a user their own account, accepting per-IP (not global)
throttling and per-instance state, because availability of one's account outweighs
maximal attacker punishment.
