# TrueRAG Host

The single executable composition root for TrueRAG.

Responsibilities:

- load and own `appsettings*.json`
- configure host-level dependency injection
- wire middleware, routing, and hosting
- map and host controller endpoints from `TrueRag.Api`
- register module libraries such as API, Workers, Retrieval, Ingestion, Storage, and Conversations
- serve as the only deployable binary for the modular monolith

## API Pipeline Composition

`TrueRag.Host` composes API middleware and controllers by:
- registering API module services via `AddTrueRagApi()`
- applying API pipeline via `UseTrueRagApiPipeline()`
- mapping controller routes via `MapControllers()`

Health and readiness endpoints:
- `GET /health/live`
- `GET /health/ready`
- `GET /health/node-state`

Readiness returns `503` when critical dependencies are unavailable and is intentionally outside tenant/app scope enforcement.

## Admission and Backpressure Behavior

`TrueRag.Host` owns admission and overload-shedding behavior via API middleware (`ResourceGuardMiddleware`) and `ResourceMonitor`.

Request flow:
- Resource guard runs before tenant/app scope middleware.
- If node state is `Overloaded`, guarded API routes return `429` with `Retry-After`.
- If node state is `Healthy` or `Degraded`, requests continue to downstream middleware/controllers.
- Bypass routes are configured with `ResourceGuard:BypassPaths` (defaults: `/health/live`, `/health/ready`).

Node-state endpoint behavior:
- `GET /health/node-state` returns `200` for `Healthy`/`Degraded`.
- `GET /health/node-state` returns `503` for `Overloaded`.
- Payload includes state and sampled signals: memory/cpu percent, threadpool queue, active requests, WAL pressure ratio, queue depth, accept/drain rates, and reason.

## ResourceGuard Limits and Tunables

The host config section `ResourceGuard` controls thresholds and hysteresis.

Primary tunables:
- `Enabled`
- `SampleIntervalMs`
- `MemoryDegradedPercent`, `MemoryOverloadedPercent`
- `CpuDegradedPercent`, `CpuOverloadedPercent`
- `ThreadPoolQueuePerCoreDegradedThreshold`, `ThreadPoolQueuePerCoreOverloadedThreshold`
- `ActiveRequestsDegradedThreshold`, `ActiveRequestsOverloadedThreshold`
- `DrainCapacityRatioDegradedThreshold`, `DrainCapacityRatioOverloadedThreshold`
- `LiveQueueDepthDegradedThreshold`, `LiveQueueDepthOverloadedThreshold`
- `ConsecutiveSamplesForOverload`, `ConsecutiveSamplesForRecovery`
- `MinimumOverloadedDurationMs`
- `RetryAfterOverloadedSeconds`
- `BypassPaths`

Operational guidance:
- Prefer gradual threshold changes and monitor for oscillation.
- Keep `ConsecutiveSamples*` and `MinimumOverloadedDurationMs` non-zero in production to avoid flapping.
- Use `DrainCapacityRatio*` and `LiveQueueDepth*` as the primary WAL-pressure signals for ingestion protection.

## Connection Strings

`TrueRag.Host` owns runtime connection-string configuration under `ConnectionStrings`.

The current host expects:

- `DbWrite`: write-side data source (ingestion path)
- `DbRead`: read-side data source (retrieval path)

Both CrateDB and PostgreSQL use Npgsql-compatible connection string format.

Example CrateDB:

```json
{
  "ConnectionStrings": {
    "DbWrite": "Host=crate-write.local;Port=5432;Database=truerag_write;Username=truerag;Password=secret",
    "DbRead": "Host=crate-read.local;Port=5432;Database=truerag_read;Username=truerag;Password=secret"
  }
}
```

Example PostgreSQL:

```json
{
  "ConnectionStrings": {
    "DbWrite": "Host=pg-write.local;Port=5432;Database=truerag_write;Username=truerag;Password=secret",
    "DbRead": "Host=pg-read.local;Port=5432;Database=truerag_read;Username=truerag;Password=secret"
  }
}
```

## RetrievalEngine Settings

`RetrievalEngine` settings are host-owned and apply to both CrateDB and PostgreSQL deployments:

- `RequireHighFidelity`
- `FallbackToStandardRag`
- `EnableMultiHopLinking`, `MultiHopMaxNodes`
- `EnableStructuralDiffing`, `StructuralDiffMaxRequests`
- `EnableSemanticCache`, `SemanticCacheTtl`
- `EnableDistributedRateLimit`, `DistributedRateLimitRequests`, `DistributedRateLimitWindow`
- `RetrievalConfidenceWeight`, `LlmCertaintyWeight`
