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

## Request Context Scope

Guarded routes under `/api/v1/*` require:
- `tenant_id`
- `app_id`
- `collection_id`

`RequestContext` host settings:
- `TenantHeaderName`, `AppHeaderName`, `CollectionHeaderName`
- `TenantClaimType`, `AppClaimType`, `CollectionClaimType`
- `CollectionIdPattern`
- `EnableCollectionScopeAuthorization`

Collection authorization behavior:
- when enabled, `TenantScopeGuardMiddleware` invokes `ICollectionScopeAuthorizer`
- resolver failures (missing/invalid scope) return `400`
- authorization denial returns `403`

## Embedding Configuration Ownership

`TrueRag.Host` owns embedding runtime configuration and validation.

Current model/mode control:
- `Embeddings:ModeSelection` selects internal vs external embedding mode per effective scope (`tenant_id + app_id + collection_id`).
- Host composes embeddings module registrations and startup validators.
- Startup diagnostics summarize effective embedding mode/profile selections without exposing secrets.
- OpenAI external embedding configuration is validated at startup (`ApiKey`, `Endpoint`, `Model`, resilience bounds) when enabled.
- External provider credentials are upstream/host supplied configuration, never request payload fields.

Pipeline contract guarantees:
- Sync ingest remains external/precomputed-vector-only.
- Async ingest is pipeline-orchestrated embedding via WAL + queue + worker and does not accept client vectors.
- Retrieval query-vector behavior follows ingestion contract per collection scope:
  - sync/client-managed spaces use caller vectors
  - async/pipeline-managed spaces use system-generated query vectors

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

## Upstream Rollout Guidance

- Keep token identity claims stable at tenant/app level.
- Send `collection_id` per request (header or claim mapping).
- Start rollout in compatibility mode (missing payload collection uses request context), then enforce explicit payload collection when clients are upgraded.
- For existing app-only deployments, migrate data and clients collection-by-collection to avoid cross-collection ambiguity.

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

## Schema Migration Runbook

`TrueRag.Host` supports manual-first migration workflow:
- `migrate status`
- `migrate up`
- `migrate validate`

Execution model:
- `migrate ...` commands run before API startup and exit after command completion.
- Startup policy is controlled by `SchemaMigrations` config:
  - `AutoMigrateOnStartup` (default production: `false`)
  - `FailFastOnPendingMigrations` (default: `true`)

Recommended operator flow:
1. Run `migrate status` and review pending versions.
2. Run `migrate validate` and confirm no checksum drift.
3. Run `migrate up` to apply pending versions.
4. Start host process only after migration success.

Failure recovery:
- If `migrate up` fails, fix root cause and re-run `migrate up` (migrations are idempotent/guarded).
- If `migrate validate` reports drift, stop rollout and reconcile migration artifacts before proceeding.

Rollback strategy:
- Schema path is forward-only by default.
- Emergency rollback is operational (restore database backup/snapshot or revert deployment while preserving compatible schema).
- Destructive schema rollback is a separate explicit operator-approved change, not part of default migration flow.

## RetrievalEngine Settings

`RetrievalEngine` settings are host-owned and apply to both CrateDB and PostgreSQL deployments:

- `RequireHighFidelity`
- `FallbackToStandardRag`
- `EnableMultiHopLinking`, `MultiHopMaxNodes`
- `EnableStructuralDiffing`, `StructuralDiffMaxRequests`
- `EnableSemanticCache`, `SemanticCacheTtl`
- `EnableDistributedRateLimit`, `DistributedRateLimitRequests`, `DistributedRateLimitWindow`
- `RetrievalConfidenceWeight`, `LlmCertaintyWeight`

## GroundingGovernance Settings

`TrueRag.Host` owns hallucination-governance configuration under `GroundingGovernance`:

- `MinimumRetrievalConfidence`
- `MinimumEvidenceCoverage`
- `RequireCitationCompleteness`
- `AllowPartialAnswer`
- `MinimumCoverageForPartialAnswer`
- `ConflictPolicy`:
  - `Abstain`
  - `SummarizeDisagreement`
  - `PreferNewest`
  - `PreferHighestAuthority`
- `EnableVerifierPass`
- `VerifierMaxAttempts`
- `VerifierMaxElapsedMs`
- `MemoryCitationPolicy`:
  - `NonCiteable`
  - `CiteableWhenRetrievedEvidence`

Operational notes:
- grounded-route controls are deterministic and test-gated.
- verifier is optional and bounded; disable in low-latency environments if needed.
- diagnostics/logging must remain scope-safe and avoid raw sensitive prompt/evidence text.
