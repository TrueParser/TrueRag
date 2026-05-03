# Project Scope: TrueRAG API

This document defines the boundaries of the TrueRAG API to ensure the project remains focused on its goal as a high-fidelity retrieval and conversation engine.

## 🟢 In Scope

The TrueRAG API is responsible for the following core capabilities:
- **Ingestion & Storage:** Receiving parsed, high-fidelity document structures from TrueParser and storing them efficiently in CrateDB.
- **Hybrid Search Engine:** Executing vector (semantic) and keyword/metadata searches using CrateDB.
- **LLM Integration:** Orchestrating prompts and connecting to Large Language Models (OpenAI, Anthropic, local models).
- **Conversation Handling:** Managing user prompts, system prompts, and tool calling context.
- **Memory & Threads:** Persisting conversation history, managing chat sessions (threads), and scoping context to specific active documents.
- **Retrieval Engine:** The logic that decides *what* to retrieve, including advanced structural expansion (retrieving whole sections instead of partial windows) and returning high-fidelity provenance (bounding boxes, precise citations).

## 🔴 Out of Scope

To maintain focus and leverage TrueParser's capabilities, the following are explicitly excluded from this codebase:
- **Chunking:** Document chunking is expected to be handled prior to ingestion or based entirely on TrueParser's structural nodes. No arbitrary token-window chunking algorithms will be implemented here.
- **Embedding Generation:** Vector generation is considered a pipeline step before or during ingestion via external services. This API will not host ML models for embedding.
- **Re-ranking:** Cross-encoder or advanced re-ranking ML pipelines are out of scope for the initial API to maintain low latency and architectural simplicity.
- **Parsing:** TrueParser handles all document extraction, OCR, and structural analysis.

## 🌟 Differentiators (The "TrueParser" Edge)
- **Structural Context Expansion:** Retrieving logical document blocks (e.g., Section 3.2) instead of flat tokens.
- **High-Fidelity Citations:** Returning exact X/Y coordinates and page numbers for UI highlighting.
- **Table-Optimized RAG:** Preserving table layouts in JSON/Markdown into the LLM context.
