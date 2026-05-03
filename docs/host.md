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

Readiness returns `503` when critical dependencies are unavailable and is intentionally outside tenant/app scope enforcement.

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
