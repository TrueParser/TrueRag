# ADR 017: CrateDB Hybrid Search Weight Semantics

## Status
Accepted

## Context
TrueRAG supports hybrid retrieval over dense vectors and full-text search.
CrateDB and PostgreSQL do not expose identical weighting mechanics.
We need explicit, non-ambiguous behavior for client-supplied hybrid weights.
CrateDB documentation for `MATCH`/multi-match behavior is a valid reference pattern for text-side weighting and field boosting semantics.

## Decision
1. **Client-supplied API parameters**
- Hybrid requests may include client-supplied weighting parameters (for example `vectorWeight`, `textWeight`, optional `rrfK`).
- API validation enforces range constraints and normalization/default behavior.

2. **CrateDB full-text weighting**
- CrateDB full-text weighting is applied via `MATCH` boost syntax on fields.
- Text-side weighting semantics are mapped to `MATCH`/boost behavior where applicable.

3. **CrateDB vector weighting**
- CrateDB `knn_match(vector, query_vector, k)` does not accept explicit weight parameters.
- Vector contribution is derived from similarity-driven `_score` behavior.
- Therefore, vector weighting is applied at fusion stage (rank/score combination), not as a direct `knn_match` argument.

4. **Fusion behavior**
- CrateDB may use SQL-side fusion query execution.
- PostgreSQL uses split-query retrieval (vector lane + text lane) with application-layer fusion.
- Both engines must honor the same API-level weighting semantics and deterministic tie-breaking policy.

5. **Scope safety**
- Tenant/app/ACL/fidelity predicates remain mandatory and identical across all hybrid lanes and engines.

6. **Engine selection ownership**
- Database engine selection is an explicit host/runtime configuration concern (read/write engine), not inferred from connection-string shape.
- Hybrid routing must bind to configured read-engine strategy:
  - `CrateDb` => SQL-side fusion path.
  - `PostgreSql` => split-lane + application-layer RRF path.

7. **Reference-pattern adoption boundaries**
- We may adopt CrateDB `MATCH` capabilities (multi-field match modes and boosting) for text-lane weighting semantics.
- For TrueRAG hybrid (text + vector), vector weighting remains fusion-stage logic and is not passed as a direct `knn_match` weight.
- Any adopted pattern must preserve mandatory tenant/app/collection/ACL/fidelity predicates.

## Consequences
### Positive
- Removes ambiguity in how weights are interpreted per engine.
- Preserves consistent API semantics despite engine differences.
- Avoids unsupported assumptions about `knn_match` weighting inputs.
- Prevents accidental engine misrouting in mixed PostgreSQL-compatible deployments.
- Leverages documented CrateDB search behavior without weakening TrueRAG invariants.

### Negative
- Requires dual-path implementation details (CrateDB SQL-side fusion vs PostgreSQL app-layer fusion).
- Requires careful test coverage to guarantee cross-engine behavior parity.
