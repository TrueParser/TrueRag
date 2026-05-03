# ADR 002: Conversation & Memory Management

## Status
Proposed

## Context
Standard RAG systems often suffer from "context bloat" where the entire chat history is naively appended to the LLM prompt, eventually exceeding token limits or causing the LLM to lose focus. Furthermore, TrueRAG needs to track which documents/sections a user is actively discussing.

## Decision
We will implement a **Stateful, Document-Scoped Memory Module**.
1. **Thread Persistence:** All user interactions will be saved in CrateDB grouped by `ThreadId`.
2. **Active Context Scoping:** The memory module will maintain an "Active Document/Section State" for each thread. If the retrieval engine determines a user is asking about "Section 3.2", this state is pinned to the thread memory.
3. **Summarized History:** Instead of raw appending, older messages in a thread will be periodically summarized by the LLM in the background and stored as a `Thread_Summary` to save tokens.

## Consequences
### Positive
- Prevents token limit exhaustion.
- Highly contextual responses because the system "remembers" the structural location the user is focused on.
- Reduced LLM costs due to smaller, summarized historical prompts.

### Negative
- Increased complexity in the `TrueRag.Conversations` module.
- Background tasks required for thread summarization.
- Requires additional database tables to store `ThreadState` and `MessageHistory`.
