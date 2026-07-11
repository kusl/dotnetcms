---
status: accepted
date: 2026-03-16
deciders: Kushal (with AI assistance from Claude and Gemini)
tags: [observability, opentelemetry, telemetry, vendor-neutral]
---

# 0015 — OpenTelemetry with vendor-neutral OTLP export to Honeycomb

## Context and Problem Statement

The app needs traces, metrics, and logs to diagnose production behavior. The chosen
backend is Honeycomb, but vendor lock-in at the instrumentation layer is undesirable
— we may change backends. Also, telemetry must be **off by default** so a fresh
clone or a local run doesn't try to export anywhere. How do we instrument, export,
and gate observability?

## Decision Drivers

- Standard, portable instrumentation — no proprietary SDK woven through the code.
- Ship to Honeycomb today without being *coupled* to Honeycomb.
- **Conditional enablement**: export only when explicitly configured.
- Respect the dependency budget (ADR-0006) — vendor-neutral packages only.
- Local, always-available diagnostics that don't depend on a network backend.

## Considered Options

1. **OpenTelemetry SDK + vendor-neutral OTLP exporter**, pointed at Honeycomb via
   its OTLP ingest and the `x-honeycomb-team` header. Backend is a config value.
2. **Honeycomb's own SDK / distro.** Turnkey, but couples the codebase to one vendor
   and violates ADR-0006's neutrality preference.
3. **No telemetry / ad-hoc logging only.** Insufficient for real diagnosis.

## Decision Outcome

Chosen option: **OpenTelemetry with the vendor-neutral OTLP exporter.** The app uses
only `OpenTelemetry.*` packages (notably
`OpenTelemetry.Exporter.OpenTelemetryProtocol`) — **no Honeycomb SDK**. Traces,
metrics, and logs are exported over OTLP; Honeycomb is reached purely by
configuration: its OTLP endpoint plus an `x-honeycomb-team` header carrying the API
key. The OTLP protocol is configurable, defaulting to `HttpProtobuf`.

Export is **conditionally enabled**: it is wired up only when **both** `Otlp:Endpoint`
and `Otlp:ApiKey` are configured. With neither set (the default), the app runs with
no exporter — safe for local dev and CI. The API key reaches production through the
deployment pipeline's per-target secrets (ADR-0019).

Two **custom local exporters** provide always-on diagnostics independent of the
network backend:

- `FileLogExporter` — writes log records to a file, with **rotation**.
- `DatabaseLogExporter` — writes log records into the SQLite store.

Both derive from `BaseExporter<LogRecord>` and also run as `IHostedService`; each is
registered once via `AddSingleton` and surfaced to the host with
`AddHostedService(sp => sp.GetRequiredService<...>())`, so a single instance is both
injectable and lifecycle-managed. Both **isolate their own exceptions** — a failing
exporter must never take down request handling or crash the process.

### Consequences

- Good: instrumentation is standard and portable; switching backends is a config
  change, not a code change.
- Good: no proprietary dependency; consistent with ADR-0006.
- Good: telemetry is inert until explicitly configured — no accidental egress from a
  clone, dev box, or CI.
- Good: local file/DB exporters give diagnostics even with no network backend, and
  can't crash the app.
- Neutral: `DatabaseLogExporter` writes logs into the **same SQLite database**
  (ADR-0004) that the app serves from. This is convenient but can amplify write
  load and, if log volume is high, contend with application queries; it is a feature
  flag to enable deliberately, not an always-on default, and file logging exists as
  the lighter alternative.
- Bad: custom exporters are code we own and must maintain (rotation logic, DB
  writes). Accepted as the price of neutral, self-contained local diagnostics.

## More Information

Neutrality here is a direct application of ADR-0006; secret delivery is ADR-0019.

**Y-statement:** In the context of production observability with Honeycomb as the
current backend, facing the risk of vendor lock-in and unwanted telemetry egress by
default, we decided for OpenTelemetry with a vendor-neutral OTLP exporter plus
conditionally-enabled custom file/DB log exporters and neglected Honeycomb's own SDK
and always-on export, to achieve portable instrumentation, backend-by-configuration,
and safe-by-default behavior, accepting the maintenance of custom exporters and the
write-amplification of logging into the app's own SQLite DB, because portability and
"off unless configured" outweigh the convenience of a turnkey vendor distro.
