# ADR 011: Dual-Layer Confidence Scoring

## Status
Proposed

## Context
In an Enterprise RAG system, the UI must be able to visually indicate to the user whether the AI is "guessing" or "certain". Standard RAG APIs provide no mathematical certainty.

## Decision
The TrueRAG API will mandate the calculation of a **Dual-Layer Confidence Score** on every single retrieval and generation cycle.

1. **Layer 1: Retrieval Confidence (Deterministic Math)**
   * Calculated during the CrateDB search.
   * The `knn_match` L2/Cosine distance and the `MATCH` BM25 score are combined using Reciprocal Rank Fusion (RRF).
   * This yields a normalized `RetrievalScore` (0.0 to 1.0) indicating how mathematically close the document is to the query.

2. **Layer 2: LLM Certainty (Semantic Evaluation)**
   * Every LLM System Prompt will include strict instructions: *"Evaluate whether the provided context fully answers the question. Output a metadata field `LLM_Certainty` between 0.0 and 1.0."*
   * If the LLM generates an answer but states it had to guess based on incomplete context, this score drops.

3. **API Response Contract:**
   * The final `TrueRagResponse` JSON payload must include an `overall_confidence` field (a weighted average of Layer 1 and Layer 2), enabling the frontend to color-code answers safely.

## Consequences
### Positive
- Instills deep enterprise trust in the API by exposing hallucination risk mathematically.
- If the RRF score is extremely low, the API can optionally short-circuit and return "No relevant documents found" without even spending money on an LLM call.

### Negative
- Tuning the RRF normalization curve across different embedding models and sparse index sizes is mathematically complex.
- Relies on the LLM adhering strictly to the metadata output prompt for Layer 2.
