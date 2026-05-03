# ADR 001: Hybrid Search Implementation

## Status
Proposed

## Context
We need a highly performant retrieval mechanism that can leverage both semantic meaning (vectors) and exact keyword matches (BM25 or similar) to ensure high-accuracy document retrieval.

## Decision
We will use CrateDB as our primary data store and leverage its native vector and full-text capabilities.

### 1. General-Purpose Search APIs
To maximize the utility of the TrueRAG engine, the Retrieval Module must expose **three distinct, individual search APIs** alongside the main RAG generation endpoint:
* **`/api/v1/search/vector`**: Executes pure dense vector similarity (`knn_match`).
* **`/api/v1/search/text`**: Executes pure BM25 sparse keyword matching (`MATCH`).
* **`/api/v1/search/hybrid`**: Executes mathematically combined Reciprocal Rank Fusion (RRF) search.

By exposing these individually, TrueRAG functions as a world-class, general-purpose enterprise search engine even for applications that don't need LLM generation.

**Architectural Constraint (Shared Core):** 
These three endpoints must be extremely thin wrappers over a **single shared retrieval service** (`IRetrievalService` in code). They must not branch into three entirely different database execution paths that bypass shared logic. This guarantees that **Tenant Isolation, Namespace routing, and Document-Level ACLs** are consistently applied regardless of which search method the user invokes.

### 2. Implementation Approach. 
- **Semantic Search:** We will use CrateDB's native `knn_match` functionality on `FLOAT_VECTOR` columns for dense vector retrieval.
- **Keyword Search:** We will use CrateDB's full-text search capabilities (`MATCH` predicate) on structured text columns.
- **Hybrid Scoring:** We will combine these scores using Reciprocal Rank Fusion (RRF) or linear interpolation directly within the SQL query to minimize data transfer between the database and the application.

## Consequences
### Positive
- **Single Datastore:** No need to manage a separate vector database (e.g., Pinecone, Milvus) and an operational database.
- **Metadata Filtering:** We can easily filter vector searches by high-fidelity metadata (e.g., `WHERE document_type = 'Invoice' AND knn_match(...)`) without the "pre-filter vs post-filter" dilemma seen in dedicated vector DBs.
- **Performance:** Pushing hybrid scoring down to the CrateDB level reduces network I/O.

### Negative
- Ties our hybrid search logic closely to CrateDB SQL dialects.
- Complex SQL queries for Hybrid Search + RRF must be carefully optimized and tested.
