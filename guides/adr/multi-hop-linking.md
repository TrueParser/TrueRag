# ADR 009: Multi-Hop Document Linking (Cross-Reference Resolution)

## Status
Proposed

## Context
Standard RAG frameworks fail when a retrieved chunk explicitly references another section (e.g., "As detailed in Appendix B, Table 4"). If "Table 4" didn't semantically match the user's prompt, the LLM is left blind to the referenced data. TrueRAG's product vision mandates resolving this.

## Decision
We will implement **Multi-Hop Structural Resolution** within the Retrieval Engine, driven entirely by upstream JSON metadata.

1. **Ingestion Metadata (No API Text Scanning):** TrueRAG will *never* use regex to scan text for phrases like "See Figure 2". The upstream engine (TrueParser) must extract these references and pass them in the JSON payload as an array: `referenced_node_ids: ["fig_2", "table_4"]`. TrueRAG simply stores this array in CrateDB.
2. **Retrieval "Hop" Phase (Auto-Detection):** 
   When the `IRetrievalService` executes a Hybrid Search and selects the top `N` nodes, it checks the `referenced_node_ids` property of each retrieved chunk.
   * If the array contains items, TrueRAG automatically executes a secondary batch `SELECT` against CrateDB (`SELECT * WHERE id IN (...)`) to fetch the explicitly referenced nodes.
3. **Context Injection:** The referenced nodes are appended to the LLM prompt context with a prefix (e.g., *[Referenced Context from Appendix B]*), ensuring the LLM has the complete picture.

## Consequences
### Positive
- Solves one of the most frustrating hallucinations in modern RAG (missing referenced tables/figures).
- Fully capitalizes on TrueParser's high-fidelity structural extraction.

### Negative
- Increases database read calls per query (Primary Search + 1 Hop).
- Risk of context window bloat if a node references dozens of other massive nodes (requires strict limits on the number of hops or token payload).
