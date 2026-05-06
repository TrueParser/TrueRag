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
Industry references (for example Supabase Postgres hybrid-search guidance) follow the same split-lane + weighted-RRF pattern and are considered compatible reference material for this ADR.

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

7. **Runtime strategy selection**
- Use explicit host/runtime engine configuration to select retrieval strategy.
- Do not infer PostgreSQL-vs-CrateDB behavior from connection string alone.
- When read engine is `PostgreSql`, hybrid retrieval must use this ADR's split-lane + RRF flow.

8. **Reference-pattern adoption boundaries**
- We may adopt the proven Postgres pattern of separate `fts` and `semantic` lanes plus weighted RRF with smoothing constant `k`.
- TrueRAG-specific constraints remain mandatory on top of that pattern:
  - tenant/app/collection predicates and ACL filtering must be applied consistently on both lanes before fusion.
  - fidelity/profile compatibility constraints must be preserved.
  - deterministic tie-breaking and stable ordering guarantees must be enforced.
  - host-configured lane limits/defaults and API guardrails must bound client-provided weights.

## Consequences
### Positive
- Predictable, index-efficient PostgreSQL hybrid retrieval.
- Robust fusion independent of incompatible raw score scales.
- Clear path for client-tunable weighting while preserving sane defaults.
- Leverages a widely validated Postgres hybrid pattern while preserving TrueRAG invariants.

### Negative
- More complex query/fusion pipeline than single-lane search.
- Requires careful parity testing with CrateDB path and weighted semantics.
