# TrueRAG Retrieval

`TrueRag.Retrieval` implements the shared retrieval core used by all search modes.

## Responsibilities
- Validate retrieval request scope and query shape.
- Route mode-specific execution to `IRetrievalRepository`.
- Keep search behavior consistent across vector, text, and hybrid endpoints.

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
- Hybrid mode uses SQL-side RRF fusion with shared tenant/app/ACL predicates.

## Response Provenance Contract
- Retrieval nodes expose provenance through `RetrievedNode.Provenance`.
- `Provenance.PageNumber` provides page-level citation context.
- `Provenance.BoundingBox` provides high-fidelity visual citation coordinates (`page`, `x`, `y`, `w`, `h`) when available.
- `Provenance.LogicalPath` preserves document structural context (section/paragraph lineage).
- Legacy flattened fields (`PageNumber`, `BoundingBox`, `LogicalPath`) are preserved for compatibility with existing callers.
