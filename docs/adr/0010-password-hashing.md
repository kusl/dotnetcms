---
status: accepted
date: 2025-12-28
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [security, auth, cryptography]
---

# 0010 — Password hashing via ASP.NET Core Identity `PasswordHasher`

## Context and Problem Statement

Admin credentials must be stored as verifiable hashes, never plaintext or
reversible ciphertext. Rolling one's own password hashing is a classic and
dangerous mistake (wrong algorithm, missing salt, wrong iteration count, timing
leaks). What hashing scheme should the app use, and should we pull in a dedicated
hashing library?

## Decision Drivers

- Use a vetted, salted, adaptive hashing scheme — no home-grown crypto.
- Stay within the dependency budget of ADR-0006 (prefer the framework over a new
  third-party package).
- Support long passphrases without silent truncation.
- Keep the surface testable in isolation.

## Considered Options

1. **`Microsoft.AspNetCore.Identity.PasswordHasher<TUser>`** — ships with ASP.NET
   Core, PBKDF2-HMAC-SHA256 with a per-hash salt and versioned format.
2. **BCrypt.Net / a dedicated bcrypt or argon2 package** — strong, but adds a
   dependency, and bcrypt silently truncates input past 72 bytes.
3. **Hand-written PBKDF2 over `Rfc2898DeriveBytes`** — reinventing what option 1
   already wraps correctly, including the encode/verify format.

## Decision Outcome

Chosen option: **`PasswordHasher<TUser>`** wrapped by
`MyBlog.Infrastructure/Services/PasswordService.cs` behind the Core interface
`IPasswordService` (`Hash` / `Verify`). The service is stateless and registered as
a **Singleton**. Verification returns success on both `Success` and
`SuccessRehashNeeded`, so the framework's forward-compatible rehash signal is
treated as a valid login rather than an error.

Because the Identity hasher runs the input through PBKDF2, there is **no bcrypt-style
72-byte truncation** — long passphrases are fully honored, which is why ADR-0009's
brute-force story and this one compose cleanly.

`PasswordService` exposes a parameterless constructor purely so unit tests can
instantiate it directly (it has no dependencies); this is an intentional, harmless
testability affordance, not a production seam.

### Consequences

- Good: no custom cryptography; salting, iteration count, and the versioned hash
  format are the framework's responsibility and move forward with it.
- Good: zero new dependencies — the package is already present transitively via
  ASP.NET Core.
- Good: unit-testable in isolation (`PasswordServiceTests` round-trips hash/verify
  and rejects wrong passwords).
- Neutral: PBKDF2 is less memory-hard than argon2id. Accepted for a single-admin
  blog; the login is additionally throttled (ADR-0009). Revisiting the algorithm is
  a one-class change if the threat model ever grows.

## More Information

The hasher is only ever exercised for the single seeded admin (ADR-0012) and via
the change-password flow. See ADR-0011 for how the verified identity becomes a
cookie principal.

**Y-statement:** In the context of storing admin credentials, facing the risk of
home-grown crypto and an extra dependency, we decided for ASP.NET Core Identity's
`PasswordHasher` and neglected bcrypt/argon2 packages and hand-rolled PBKDF2, to
achieve salted adaptive hashing with no truncation and no new dependency, accepting
PBKDF2's weaker memory-hardness, because the framework primitive is correct, free,
and sufficient for this threat model.
