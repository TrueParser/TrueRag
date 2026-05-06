# TrueRAG API

HTTP/API module for request context resolution and controller-based endpoint orchestration.

## Controller Surface

Endpoints are implemented with ASP.NET Core controllers in `TrueRag.Api/Controllers` and composed by `TrueRag.Host`.

## API Layer Structure

`TrueRag.Api` uses the following boundary structure:
- `Controllers/` for HTTP transport endpoints
- `Services/` for API-level orchestration adapters
- `Helpers/` for shared HTTP result/error mapping
- `Middleware/` for correlation, exception normalization, and tenant/app guard
- `Extensions/` for DI and app-pipeline composition
- `Models/` for API-facing response contracts
- `ResourceGuard/` reserved for API-boundary admission components

## API Pipeline Order

Host composes API middleware in this order:
1. `GlobalExceptionMiddleware`
2. `CorrelationIdMiddleware`
3. `ResourceGuardMiddleware`
4. `TenantScopeGuardMiddleware`
5. Controller dispatch

## Search Endpoints
- `POST /api/v1/search/vector`
- `POST /api/v1/search/text`
- `POST /api/v1/search/hybrid`

All search endpoints route through `IRetrievalService` and enforce tenant/app/collection/ACL scope via request context.

Vector/Hybrid query embedding behavior:
- Sync/client-managed embedding contract: query must include precomputed `QueryVector` (and `QueryText` for text/hybrid behavior).
- Async/pipeline-managed embedding contract: query uses `QueryText`; system generates `QueryVector`.
- Descriptor mismatch is rejected with deterministic error `retrieval.embedding_space_mismatch`.

Hybrid request contract (`POST /api/v1/search/hybrid`):
- `queryText` (required)
- `queryVector` (required for external/client-managed embedding mode; generated internally for internal mode)
- `topK` (required)
- Optional fusion inputs:
  - `vectorWeight` (default `1.0`)
  - `textWeight` (default `1.0`)
  - `rrfK` (default `60`)

Hybrid validation and guardrails:
- Reject mode (default): invalid ranges return deterministic validation errors:
  - `retrieval.hybrid_vector_weight_invalid`
  - `retrieval.hybrid_text_weight_invalid`
  - `retrieval.hybrid_rrfk_invalid`
  - `retrieval.hybrid_weight_sum_invalid`
- Clamp mode: out-of-range inputs are clamped to configured bounds before fusion.

Search responses return `RetrievalResponse` with `nodes[]`. Each node includes:
- `nodeId`, `documentId`, `nodeType`, `text`, `score`, `fidelityLevel`
- legacy provenance fields: `pageNumber`, `boundingBox`, `logicalPath`
- canonical provenance contract: `provenance`
  - `pageNumber`
  - `boundingBox` (`page`, `x`, `y`, `w`, `h`) for high-fidelity sources
  - `logicalPath`

Hybrid execution strategy routing:
- Host config explicitly selects storage engines with:
  - `Storage:WriteEngine` (`CrateDb` or `PostgreSql`)
  - `Storage:ReadEngine` (`CrateDb` or `PostgreSql`)
- Host does not infer engine from connection-string format.
- `RetrievalEngine:HybridFusionMode`:
  - `Auto` (default): derives mode from `Storage:ReadEngine`
    - `CrateDb` => `Sql`
    - `PostgreSql` => `SplitRrf`
  - `Sql`: repository SQL-side fusion path
  - `SplitRrf`: retrieval-layer vector/text lane fusion

## Ingestion Endpoints
- `POST /api/v1/ingest/async`
- `POST /api/v1/ingest/sync`

Embedding execution behavior:
- `/api/v1/ingest/sync` is strict precomputed-vector-only mode.
- `/api/v1/ingest/sync` never executes internal embeddings.
- `/api/v1/ingest/async` is pipeline-orchestrated embedding path (WAL + queue + worker).
- `/api/v1/ingest/async` does not accept client-provided vectors.
- Async worker provider execution is descriptor-driven (ONNX internal or OpenAI external).
- Effective scope is always `tenant_id + app_id + collection_id`.

Deterministic validation errors:
- Missing sync vectors: `ingestion.sync_precomputed_vectors_required`.
- Sync call against internal-mode collection: `ingestion.sync_disabled_for_internal_embedding_mode`.

## Conversation and RAG Endpoints
- `GET /api/v1/context`
- `POST /api/v1/conversations/threads/{threadId}/turns`
- `GET /api/v1/conversations/threads/{threadId}?take={n}`
- `POST /api/v1/conversations/threads/{threadId}/refresh?recentWindow={n}`
- `POST /api/v1/rag/generate`

Grounded route policy:
- `/api/v1/rag/generate` is grounded-only mode.
- Utility mode is rejected with deterministic validation error: `conversation.grounded_route_requires_grounded_mode`.

Grounded response semantics (`ConversationReply`):
- `assistantMessage`
- `claims[]`
- `citations[]`
- `insufficiencyReason`
- `groundingStatus`:
  - `Grounded`
  - `PartiallyGrounded`
  - `InsufficientEvidence`
  - `ConflictingEvidence`
  - `ValidationFailed`
- `diagnostics`:
  - `retrievalHitCount`
  - `selectedEvidenceNodeIds`
  - `citationValidationResult`
  - `verifierOutcome` (`pass`, `revise`, `reject`, `not_applicable`)
  - `abstentionReason`
  - `verifierRetryCount`
  - `promptInjectionDetected`

Deterministic governance failure codes include:
- `schema_invalid.*`
- `citation_invalid.*`
- `insufficient_evidence.*`
- `verifier_reject`
- `prompt_injection_detected`

## Health Endpoints
- `GET /health/live`
- `GET /health/ready`

Health behavior:
- `live` returns `200` when process is running.
- `ready` returns `200` when critical dependencies are ready.
- `ready` returns `503` when any critical dependency is unavailable.
- health routes are anonymous and excluded from tenant/app scope guard requirements.

## Collection Scope Enforcement

- Guarded API routes (`/api/v1/*`) require `tenant_id`, `app_id`, and `collection_id`.
- `collection_id` is resolved from claim/header and validated against configured pattern.
- Optional collection authorization hook (`ICollectionScopeAuthorizer`) can return `403` for disallowed collection access.

This project is a module library used by `TrueRag.Host`, not a standalone entrypoint.
