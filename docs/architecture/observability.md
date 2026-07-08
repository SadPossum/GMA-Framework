# Observability

Observability is split into vendor-neutral module instrumentation and optional host/deployment adapters.

## Design Rules

- Module code uses `ILogger<T>`, `IMeterFactory`, and `ActivitySource`.
- Module code never references Prometheus, Loki, Grafana, Serilog sinks, or OpenTelemetry exporters.
- `ServiceDefaults` owns exporter configuration.
- HTTP hosts such as `Host.Api` and `Host.AdminApi` own Serilog appsettings and compose the explicit `Gma.Framework.Logging.Serilog` and `Gma.Framework.Api.Serilog` adapters.
- Prometheus and OTLP export are independently optional.

## Meter and Source Names

All application-owned meters and activity sources use the configured `{application-namespace}.*` prefix. The default `ApplicationIdentity:Namespace` is `gma`.

Current meters:

```text
{application-namespace}.application
{application-namespace}.caching
{application-namespace}.messaging
```

Future module example:

```text
{application-namespace}.billing
```

`ServiceDefaults` subscribes to `{application-namespace}.*`, so new module meters do not require exporter changes after the application namespace is configured.

## Current Metrics

CQRS:

- `{application-namespace}.commands.executed`
- `{application-namespace}.commands.duration`
- `{application-namespace}.queries.executed`
- `{application-namespace}.queries.duration`

Outbox:

- `{application-namespace}.outbox.claimed`
- `{application-namespace}.outbox.published`
- `{application-namespace}.outbox.failed`
- `{application-namespace}.outbox.publish.duration`

Inbox:

- `{application-namespace}.inbox.messages`
- `{application-namespace}.inbox.process.duration`

Caching:

- `{application-namespace}.cache.requests`
- `{application-namespace}.cache.duration`
- `{application-namespace}.cache.backend.failures`
- `{application-namespace}.cache.invalidation.failures`

Common metric tags:

- `module`
- `operation`
- `provider`
- `result`
- `error.code`
- `messaging.destination.name`

Do not put tenant ids, user ids, message ids, tokens, URLs with ids, or other unbounded values in metrics.

## Structured Log Properties

Common log properties:

- `Module`
- `Operation`
- `Result`
- `ErrorCode`
- `TenantId`
- `TraceId`
- `MessageId`
- `Subject`

Tenant and message identifiers are allowed in logs because logs are event records, not metric dimensions.

## Module Endpoint Metadata

Module route groups attach module metadata:

```csharp
RouteGroupBuilder group = endpoints.MapGroup("/api/auth")
    .WithModuleName(this.Name)
    .WithTags("Auth");
```

HTTP hosts call `UseConfiguredSerilog()` from `Gma.Framework.Logging.Serilog` to load their local Serilog appsettings, then call `UseGmaSerilogRequestLogging()` from `Gma.Framework.Api.Serilog`. The request logging adapter reads endpoint metadata and enriches Serilog request events with module, tenant, and trace properties while hosts still own levels, sinks, and deployment-specific configuration data.

`Gma.Framework.Api` remains vendor-neutral and owns only HTTP/module metadata. Serilog-specific request logging lives in the separate adapter project so modules can depend on generic API contracts without inheriting logging backend packages.
Serilog host configuration lives in `Gma.Framework.Logging.Serilog`, so host projects do not reference Serilog packages directly.

## Prometheus

Prometheus export is optional:

```json
{
  "Observability": {
    "Prometheus": {
      "Enabled": true,
      "EndpointPath": "/metrics"
    }
  }
}
```

Development enables `/metrics`. The base configuration keeps it disabled so production deployments opt in explicitly.

The application uses the stable `prometheus-net.AspNetCore` adapter to expose standard `System.Diagnostics.Metrics` data. Modules remain independent of that package.

The application exposes a scrape endpoint; it does not require a Prometheus server in the default Aspire AppHost.
`Observability:Prometheus:EndpointPath` is validated at startup and must be an absolute app path such as `/metrics`.

## OTLP and Loki

OTLP export is optional and disabled by default:

```json
{
  "Observability": {
    "Otlp": {
      "Enabled": true,
      "Endpoint": "http://alloy:4318",
      "ExportMetrics": true,
      "ExportTraces": true,
      "ExportLogs": true
    }
  }
}
```

The exporter uses OTLP over HTTP/protobuf.
When OTLP is enabled, at least one signal must be exported. A configured OTLP endpoint must be an absolute HTTP or HTTPS URI.

Recommended Loki path:

```text
ILogger / Serilog scopes
  -> OpenTelemetry OTLP logs
  -> Grafana Alloy or OpenTelemetry Collector
  -> Loki
```

This keeps Loki out of module and host contracts. A deployment can replace Loki without changing application code.

## Adding Module Metrics

Create a module-owned internal metrics class using `IMeterFactory`:

```csharp
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime;

internal sealed class BillingMetrics
{
    private readonly Counter<long> invoicesCreated;

    public BillingMetrics(IMeterFactory meterFactory, IOptions<ApplicationIdentityOptions> applicationIdentity)
    {
        string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;
        Meter meter = meterFactory.Create($"{applicationNamespace}.billing");
        this.invoicesCreated = meter.CreateCounter<long>($"{applicationNamespace}.billing.invoices.created");
    }
}
```

Register it in the module and keep tags bounded.

## Local Stack

The default AppHost does not run Prometheus, Loki, or Grafana. Aspire console telemetry and `/metrics` are enough for normal development.

Add a separate opt-in observability deployment profile when dashboards, alert rules, retention, or multi-instance log search become real requirements.
