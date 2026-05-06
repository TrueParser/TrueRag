# ADR 001: Hybrid Search Implementation

## Status
Accepted (Amended by ADR 017 and ADR 018)

## Context
We need a highly performant retrieval mechanism that can leverage both semantic meaning (vectors) and exact keyword matches (BM25 or similar) to ensure high-accuracy document retrieval.

## Decision
TrueRAG exposes a single hybrid-search API contract with engine-specific execution strategies selected by runtime configuration.
CrateDB remains the primary optimized path, and PostgreSQL/pgvector is a supported hybrid execution path with equivalent API semantics.

### 1. General-Purpose Search APIs
To maximize the utility of the TrueRAG engine, the Retrieval Module must expose **three distinct, individual search APIs** alongside the main RAG generation endpoint:
* **`/api/v1/search/vector`**: Executes pure dense vector similarity (`knn_match`).
* **`/api/v1/search/text`**: Executes pure BM25 sparse keyword matching (`MATCH`).
* **`/api/v1/search/hybrid`**: Executes mathematically combined Reciprocal Rank Fusion (RRF) search.

By exposing these individually, TrueRAG functions as a world-class, general-purpose enterprise search engine even for applications that don't need LLM generation.

**Architectural Constraint (Shared Core):**
These three endpoints must be extremely thin wrappers over a **single shared retrieval service** (`IRetrievalService` in code). They must not branch into three entirely different database execution paths that bypass shared logic. This guarantees that **Tenant Isolation, Namespace routing, and Document-Level ACLs** are consistently applied regardless of which search method the user invokes.

### 2. Implementation Approach. 
- **Semantic Search:** Use engine-native vector search primitives (`knn_match` on CrateDB, pgvector distance ordering on PostgreSQL).
- **Keyword Search:** Use engine-native full-text search primitives (`MATCH` on CrateDB, `tsvector/tsquery` on PostgreSQL).
- **Hybrid Scoring:** Use deterministic fusion semantics (RRF baseline with optional client weights), with:
  - CrateDB: SQL-side fusion path.
  - PostgreSQL: split-lane (FTS + vector) and fusion.
- **Routing Rule:** Engine strategy is selected by explicit host runtime engine configuration; it must not be inferred from connection-string shape.

### 3. ADR Relationship
- ADR 017 defines CrateDB-specific weighting semantics and routing constraints.
- ADR 018 defines PostgreSQL + pgvector split-lane RRF semantics and routing constraints.
- When conflicts occur on engine-specific behavior, ADR 017/018 are authoritative refinements for this ADR.

## Consequences
### Positive
- **Single API Contract:** Clients use one retrieval contract while runtime chooses the engine-specific strategy.
- **Metadata Filtering:** We can easily filter vector searches by high-fidelity metadata (e.g., `WHERE document_type = 'Invoice' AND knn_match(...)`) without the "pre-filter vs post-filter" dilemma seen in dedicated vector DBs.
- **Performance Flexibility:** CrateDB can keep SQL-side fusion while PostgreSQL uses index-safe split-lane + RRF.

### Negative
- Requires maintaining dual execution details for CrateDB and PostgreSQL while preserving parity.
- Requires careful test coverage to guarantee cross-engine behavior parity.
