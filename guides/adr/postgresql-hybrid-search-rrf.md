# ADR 018: PostgreSQL Hybrid Search via FTS + pgvector + RRF

## Status
Accepted

## Context
PostgreSQL does not provide a native single hybrid-search operator equivalent to a dedicated `HYBRID_MATCH(...)` command.
Hybrid retrieval must be composed from:
- PostgreSQL full-text search (`tsvector`, `tsquery`, `ts_rank_cd`, GIN index)
- pgvector vector search (`vector`, distance operators, HNSW/IVFFlat index)
- application/SQL fusion logic (RRF).

Ranking scales differ between FTS rank values and vector distance/similarity, so direct raw score addition is not robust.

## Decision
1. **Retrieval architecture**
- Execute two indexed retrieval lanes:
  - FTS lane (`search_vector @@ query`, ranked by `ts_rank_cd`).
  - Vector lane (`ORDER BY embedding <=> query_embedding`, index-friendly top-N).
- Fuse lane results using Reciprocal Rank Fusion (RRF), not raw score arithmetic.

2. **Baseline fusion defaults (v1)**
- `fts_weight = 1.0`
- `vector_weight = 1.0`
- `rrf_k = 60`
- `candidate_limit = 100` per lane

3. **Optional weighted fusion**
- API may provide optional `fts_weight` and `vector_weight`.
- Weighted formula:
  - `(fts_weight * 1/(rrf_k + fts_rank)) + (vector_weight * 1/(rrf_k + vector_rank))`
- Missing lane ranks contribute `0.0`.

4. **Index-safe pgvector rule**
- Vector lane must use direct distance ordering:
  - `ORDER BY embedding <=> :query_embedding LIMIT :candidate_limit`
- Do not wrap distance in transformed expressions for ordering when index usage is required.

5. **Schema/index requirements**
- Store a `tsvector` search column (stored/generated or maintained) for production retrieval.
- Required indexes:
  - GIN on `search_vector`
  - HNSW/IVFFlat on embedding with appropriate operator class
  - scope index on `tenant_id`, `app_id`

6. **Scope predicates**
- Tenant/app/ACL/fidelity filters are mandatory and equivalent across both lanes prior to fusion.

## Consequences
### Positive
- Predictable, index-efficient PostgreSQL hybrid retrieval.
- Robust fusion independent of incompatible raw score scales.
- Clear path for client-tunable weighting while preserving sane defaults.

### Negative
- More complex query/fusion pipeline than single-lane search.
- Requires careful parity testing with CrateDB path and weighted semantics.
