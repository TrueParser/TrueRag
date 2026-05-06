# TrueRAG Retrieval

`TrueRag.Retrieval` implements the shared retrieval core used by all search modes.

## Responsibilities
- Validate retrieval request scope and query shape.
- Route mode-specific execution to `IRetrievalRepository`.
- Keep search behavior consistent across vector, text, and hybrid endpoints.
- Apply optional advanced retrieval features behind config gates.
- Enforce embedding-mode aware query requirements and descriptor compatibility.

## Public API Surface
- `RetrievalModule.AddTrueRagRetrieval(IServiceCollection)`
- `IRetrievalService.SearchVectorAsync(...)`
- `IRetrievalService.SearchTextAsync(...)`
- `IRetrievalService.SearchHybridAsync(...)`

Guide naming note:
- Older ADR text may use `IRetrievalEngine` as a conceptual name.
- Implemented code uses `IRetrievalService` (orchestration) and `IRetrievalRepository` (storage query boundary).

## Endpoint Integration
The host maps these endpoints to the shared retrieval service:
- `POST /api/v1/search/vector`
- `POST /api/v1/search/text`
- `POST /api/v1/search/hybrid`

## Embedding Contract Behavior

Mode resolution:
- Effective scope is `tenant_id + app_id + collection_id`.
- Retrieval resolves active mode from collection-scoped embedding settings.

Vector/Hybrid requests:
- Sync-ingested collections (client-managed embedding space) require caller-provided `QueryVector`.
- Async-ingested collections (pipeline-managed embedding space) use `QueryText` and system-generated query vectors.
- Provided or generated query vectors are validated against active descriptor dimensions.
- Mismatch is rejected with `retrieval.embedding_space_mismatch`.

## Storage Query Semantics
- Vector mode uses `knn_match` (CrateDB dialect).
- Text mode uses `MATCH` (CrateDB dialect).
- Hybrid supports two execution modes:
  - `Sql`: repository-managed SQL-side fusion.
  - `SplitRrf`: retrieval-layer fusion over separate vector and text lanes.
- PostgreSQL lane semantics:
  - Vector lane uses index-safe ordering (`ORDER BY vector <=> :query_vector`) with bounded candidate limit.
  - Text lane uses `search_vector @@ websearch_to_tsquery(...)` with `ts_rank_cd` ranking.
- CrateDB path keeps SQL-side hybrid behavior with text-side `MATCH` semantics and fusion-stage vector weighting.
- Tenant/app/collection/ACL/fidelity predicates remain mandatory on all lanes.

## Hybrid Fusion Configuration
- `RetrievalEngine:HybridFusionMode`
  - `Auto` (default): selected from read engine (`PostgreSql => SplitRrf`, `CrateDb => Sql`)
  - `Sql`
  - `SplitRrf`
- `RetrievalEngine:HybridCandidateLimit` (default `100`) for split-lane candidate breadth.
- Fusion defaults:
  - `HybridDefaultVectorWeight` (`1.0`)
  - `HybridDefaultTextWeight` (`1.0`)
  - `HybridDefaultRrfK` (`60`)
- Guardrails:
  - `HybridGuardrailMode`: `Reject` or `Clamp`
  - `HybridMinWeight` / `HybridMaxWeight`
  - `HybridMinRrfK` / `HybridMaxRrfK`

## Hybrid Fusion Determinism
- RRF baseline uses `1/(k+rank)` with configured `k`.
- Weighted RRF applies `vectorWeight` and `textWeight`.
- Stable tie-break uses deterministic node-id ordering.
- Zero-lane safeguards:
  - if one lane is empty, the non-empty lane is returned
  - if both lanes are empty, response nodes are empty

## Hybrid Diagnostics
- Retrieval emits structured hybrid split diagnostics logs including:
  - effective weights and `rrfK`
  - lane candidate limit and lane hit counts
  - vector/text overlap ratio
- Retrieval records hybrid split metrics:
  - `truerag_hybrid_split_calls_total`
  - `truerag_hybrid_vector_weight`
  - `truerag_hybrid_text_weight`
  - `truerag_hybrid_lane_overlap_ratio`

## Advanced Retrieval Behaviors (Phase 3.3)
- Multi-hop linking: if a retrieved node contains `ReferencedNodeIds`, the retrieval service fetches those referenced nodes in a bounded second hop.
- Structural diffing: when `document_group_id`, `left_version`, `right_version`, and `logical_path` filters are provided, retrieval attaches deterministic version diffs.
- Dual-layer confidence: responses include retrieval confidence and overall confidence fields.
- Feature gates are controlled under `RetrievalEngine` options in host configuration.

## Grounding-Focused Retrieval Responsibilities (Phase 10)

Retrieval output now feeds grounded-generation governance with citation-safe evidence packaging requirements:

- Evidence items are consumed as citeable units with stable node identity.
- Collection scope and ACL isolation remain mandatory before any citation can be considered valid.
- Retrieved context supports optional span metadata (`span_id`, offsets) used by citation validators.
- Source freshness (`sourceUpdatedAtUtc`) and authority (`sourceAuthorityScore`) can participate in contradiction-resolution policy.

Grounding coordination points:
- answerability gate consumes retrieval score/coverage signals before generation.
- contradiction policy consumes conflicting retrieved evidence before and/or after generation.
- citation validator rejects out-of-scope, ACL-invalid, or unsupported span citations.

## Stateless Redis Capabilities (ADR 008)
- Semantic cache is tenant/app/collection isolated by key design: `retrieval:semantic:{tenant_id}:{app_id}:{collection_id}:{hash}`.
- Distributed limiter store is tenant/app/collection isolated by lane: `retrieval:ratelimit:{tenant_id}:{app_id}:{collection_id}:{lane}`.
- Both features are optional and controlled by `RetrievalEngine` flags.

## Response Provenance Contract
- Retrieval nodes expose provenance through `RetrievedNode.Provenance`.
- `Provenance.PageNumber` provides page-level citation context.
- `Provenance.BoundingBox` provides high-fidelity visual citation coordinates (`page`, `x`, `y`, `w`, `h`) when available.
- `Provenance.LogicalPath` preserves document structural context (section/paragraph lineage).
- Legacy flattened fields (`PageNumber`, `BoundingBox`, `LogicalPath`) are preserved for compatibility with existing callers.
- Extended fields include `DocumentGroupId`, `VersionNumber`, and `ReferencedNodeIds`.
- Retrieval response now carries `RetrievalConfidence`, `OverallConfidence`, and optional `Diffs`.
