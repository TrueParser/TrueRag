# ADR 005: Advanced Retrieval Engine

## Status
Proposed

## Context
Standard RAG pipelines use overlapping token windows to capture context, which destroys the logical structure of a document (breaking tables, crossing unrelated sections). Furthermore, standard RAG struggles with exact citation tracking. Since TrueParser provides high-fidelity structural nodes and coordinates, the TrueRAG Retrieval Engine needs an architecture that exploits this.

## Decision
We will build a **Structurally Aware Retrieval Engine** that implements the following patterns:

### 1. Structural Context Expansion
Instead of `Chunk + N Tokens`, our database schema will maintain parent-child relationships (e.g., `Document -> Section -> Paragraph`). 
When a vector search matches a `Paragraph` node, the Retrieval Engine will query CrateDB for the `Parent Section` ID and retrieve the entire logical block to feed to the LLM. 

### 2. High-Fidelity Provenance
The database schema for `Nodes` will include fields for `PageNumber`, `BoundingBox_X`, `BoundingBox_Y`, `Width`, and `Height`.
When the LLM formulates a response based on retrieved nodes, the Conversation Module will attach this exact coordinate data to the final API Response, allowing the frontend UI to draw highlight boxes directly on the source PDF.

### 3. Table-Aware Injection
Nodes of type `Table` will store their content natively as JSON arrays in CrateDB's `OBJECT` type, rather than flattened text strings. 
When a `Table` node is retrieved by the Hybrid Search, the Retrieval Engine will serialize it into strict Markdown or JSON format before prompt injection, preserving the column and row axis completely.

## Consequences
### Positive
- Massive reduction in LLM hallucinations, particularly for complex tabular data.
- "Wow factor" in the UI by enabling visual citations on the original PDFs.
- Provides a direct, tangible showcase of *why* TrueParser's parsing quality matters.

### Negative
- Increases database schema complexity (need to model hierarchical trees in CrateDB).
- Expanding to parent nodes might consume more context window tokens if sections are extremely large (will need safeguards/limits on node size expansion).
