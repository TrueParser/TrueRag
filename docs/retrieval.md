# TrueRAG Retrieval

`TrueRag.Retrieval` implements the shared retrieval core used by all search modes.

## Responsibilities
- Validate retrieval request scope and query shape.
- Route mode-specific execution to `IRetrievalRepository`.
- Keep search behavior consistent across vector, text, and hybrid endpoints.
- Apply optional advanced retrieval features behind config gates.

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

## Storage Query Semantics
- Vector mode uses `knn_match` (CrateDB dialect).
- Text mode uses `MATCH` (CrateDB dialect).
- Hybrid mode uses SQL-side RRF fusion with shared tenant/app/collection/ACL predicates.
- The PostgreSQL path uses pgvector and full-text SQL equivalents with the same tenant/app/collection/ACL predicate behavior.

## Advanced Retrieval Behaviors (Phase 3.3)
- Multi-hop linking: if a retrieved node contains `ReferencedNodeIds`, the retrieval service fetches those referenced nodes in a bounded second hop.
- Structural diffing: when `document_group_id`, `left_version`, `right_version`, and `logical_path` filters are provided, retrieval attaches deterministic version diffs.
- Dual-layer confidence: responses include retrieval confidence and overall confidence fields.
- Feature gates are controlled under `RetrievalEngine` options in host configuration.

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
