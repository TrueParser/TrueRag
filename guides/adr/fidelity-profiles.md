# ADR 006: Configurable Fidelity Profiles (Graceful Degradation)

## Status
Proposed

## Context
While TrueRAG is optimized to showcase TrueParser's High-Fidelity output (Structural Expansion, Table Math, Visual Citations), the API must remain robust. If a user ingests a document processed by a "normal extractor" (e.g., a flat array of text chunks without hierarchical tree IDs or bounding boxes), the retrieval engine must not crash or fail. It needs to gracefully degrade to standard RAG behaviors.

**Clarification on Chunking:** As stated in `Scope.md`, TrueRAG does *not* perform token-chunking or embedding generation itself. All data (whether high-fidelity or low-fidelity) arrives pre-chunked/embedded from upstream. The difference lies in the *structure* of that incoming data.

## Decision
We will implement **Fidelity Profiles** configured at the Ingestion and Retrieval layers.

1. **Ingestion Level (Auto-Detection):**
   When a document payload is ingested, the system will **auto-detect** its `FidelityLevel` by inspecting the payload structure (e.g., presence of bounding boxes or parent IDs).
   * `High` (TrueParser): Payload contains parent-child hierarchy IDs, bounding boxes, and native table JSON.
   * `Standard` (Normal Extractor): Payload contains only a flat array of pre-embedded text chunks with basic metadata.
   
   *Override Mechanism:* To support edge cases, migrations, and testing, the API request contract will allow an optional `fidelity` field (`auto` | `high` | `standard`). It defaults to `auto`.

2. **Retrieval Engine Profiles:**
   The `IRetrievalService` will check the document's `FidelityLevel` before executing advanced features:
   * **Structural Expansion:** If `FidelityLevel == High`, fetch parent nodes (e.g., the whole Section). If `Standard`, fallback to fetching `N` adjacent sequential chunks since no tree structure exists.
   * **Visual Citation:** If `FidelityLevel == High`, return X/Y coordinates. If `Standard`, return only the Document ID and Page Number (if available).
   * **Analytical RAG (SQL):** If `FidelityLevel == High` and the query is tabular, route to SQL. If `Standard`, fallback to standard vector similarity search.

3. **Global Configuration:**
   The fallback behaviors can be toggled in `src/TrueRag.Host/appsettings.json`:
   ```json
   {
     "RetrievalEngine": {
       "RequireHighFidelity": false,
       "FallbackToStandardRag": true
     }
   }
   ```

## Consequences
### Positive
- **Resolves Scope Conflict:** TrueRAG remains strictly out of the chunking/embedding business, but can still serve flat data from legacy upstream extractors.
- **Broad Market Appeal:** The API remains useful even for legacy datasets.
- **Robustness:** No null-reference exceptions or crashes when hierarchical parent IDs or bounding boxes are missing.

### Negative
- **Increased Code Complexity:** The Retrieval Engine must maintain two execution paths (Tree-Traversal vs. Adjacent-Chunking) and two prompt generation strategies.
