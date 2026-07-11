---
status: accepted
date: 2026-03-16
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [ci-cd, deployment, iis, github-actions, powershell]
---

# 0019 — CI/CD: IIS WebDeploy from GitHub Actions to two targets

## Context and Problem Statement

The app is deployed to **IIS** on Windows, to **two separate sites**, from GitHub
Actions. Deployment must not corrupt a live site (locked files, half-written
content) and must inject per-target configuration and secrets — including the OTLP
telemetry settings (ADR-0015) and the admin password (ADR-0012) — without baking
them into the build. How is deployment automated safely and repeatably?

## Decision Drivers

- Deploy to IIS reliably despite file-in-use locking during publish.
- Two independent targets, each with its **own secrets**.
- Inject runtime config (OTLP, credentials) at deploy time, not build time.
- Keep the pipeline maintainable after a third-party deploy action proved
  unreliable.
- Data safety on live sites is a hard constraint.

## Considered Options

1. **WebDeploy (MSDeploy) driven by a custom PowerShell step**, with AppOffline and
   XPath-based `web.config` edits. Full control, no third-party black box.
2. **A third-party "deploy to IIS/Azure" GitHub Action.** Convenient, but was
   dropped after it proved unreliable/opaque for this setup.
3. **FTP or file-copy deploy.** Simple, but no AppOffline semantics and worse
   handling of locked files and atomicity.

## Decision Outcome

Chosen option: **WebDeploy via custom PowerShell.** The workflow
(`.github/workflows/build-deploy.yml`) builds and tests across a **ubuntu/windows/
macos matrix**, runs the **E2E job**, and then runs **two deploy jobs**:

- `deploy` — the primary target, using secrets such as `WEBSITE_NAME`,
  `HONEYCOMB_API_KEY`, etc.
- `deploynice` — the second target, using a parallel `NICE_*` secret set.

Each job invokes **msdeploy** directly from a PowerShell script (the earlier
third-party action was removed). Hard-won specifics captured so they aren't
rediscovered painfully:

- **`-enableRule:AppOffline`** is used so IIS takes the app offline during the sync,
  avoiding "file in use" failures on a live site.
- `web.config` is edited by **XPath** — `SelectSingleNode()` against nodes like
  `//aspNetCore` — to inject OTLP settings. PowerShell **dot-notation traversal
  fails** on element names containing dots (e.g. `system.webServer`), so XPath is
  mandatory, not stylistic.
- Complex msdeploy arguments are assembled as **separate variables**, not joined
  with **backtick line-continuations** — backticks mangle complex quoted arguments
  in PowerShell, producing baffling failures.
- **`WEBSITE_NAME` must be the IIS site name only** (not the full domain); using the
  domain breaks the sync target.
- Secrets are **per-target**, so the two sites never share credentials, and the
  admin password / OTLP key reach production only through these secrets.

### Consequences

- Good: reliable, atomic-feeling deploys to live IIS sites; no locked-file failures.
- Good: two targets with fully independent secrets from one pipeline.
- Good: runtime config and secrets are injected at deploy time, never committed.
- Good: no dependence on an opaque third-party action.
- Bad: the pipeline carries real PowerShell/msdeploy complexity with sharp edges
  (backticks, XPath, the site-name gotcha). Accepted, and documented here precisely
  so the edges don't cut twice.
- Neutral (cleanup, tracked in `solid-review.md`): a **stale duplicate workflow**
  exists at `src/.github/workflows/build-deploy.yml`. GitHub only reads the
  repo-root `.github`, so it is dead and harmless; removing it is recommended but not
  done here to keep this change documentation-only.

## More Information

Delivers the secrets consumed by ADR-0012 (admin password) and ADR-0015 (OTLP key);
runs the tests defined in ADR-0018.

**Y-statement:** In the context of deploying to two live IIS sites from GitHub
Actions, facing locked-file failures and the need for per-target secret injection
after a third-party action proved unreliable, we decided for WebDeploy driven by
custom PowerShell with AppOffline and XPath config edits and neglected the
third-party action and FTP/file-copy, to achieve reliable, repeatable, secret-safe
deploys to independent targets, accepting real PowerShell/msdeploy complexity and a
stale duplicate workflow file, because full control plus AppOffline is the only
combination that met the hard "don't break a live site" constraint.
