# ADR 010: Structural Diffing & Version-Aware RAG

## Status
Proposed

## Context
Enterprise users frequently ask questions like "What changed in the termination clause between the 2022 and 2023 MSA?" Standard RAG retrieves completely unaligned token chunks, making reliable comparison impossible for an LLM.

## Decision
We will implement **Version-Aware Structural Diffing** at the database layer.

1. **Schema Versioning:** Every document and node in CrateDB must track `DocumentGroupId` (e.g., "MSA_Contract") and `VersionNumber`.
2. **Logical Path Alignment:** Because TrueParser extracts a deterministic tree, a specific clause (e.g., `Section 4.1.2`) shares the exact same `LogicalNodePath` across versions.
3. **Diffing Engine:** 
   When the Conversation Module detects a comparison intent:
   * The Retrieval Engine fetches the requested `LogicalNodePath` for `Version 1`.
   * It fetches the exact same `LogicalNodePath` for `Version 2`.
   * It runs a structural string diff algorithm (e.g., Diffplex) in the `.NET` application tier.
   * It injects *only the unified diff* into the LLM prompt.

## Consequences
### Positive
- Enables flawless, deterministic contract comparison—a massive enterprise selling point.
- Prevents the LLM from hallucinating differences between unaligned text chunks.

### Negative
- Extremely dependent on TrueParser generating stable `LogicalNodePaths` across document versions. If the author drastically renumbers the document, structural alignment fails.
- Requires intent detection in the Conversation module to trigger the Diffing Engine instead of the standard Search Engine.
