# ADR 008: Stateless Retrieval API and Redis Caching

## Status
Proposed

## Context
To achieve global scale and high availability, the TrueRAG Retrieval API must be horizontally scalable behind a load balancer. This requires the API instances to hold zero local state (no in-memory caches or sticky sessions).

## Decision
The Retrieval API will be strictly **Stateless**. All ephemeral state will be offloaded to **Redis**.

1. **Conversation State:** Short-term thread context (e.g., "Active Document Scope") will be held in Redis. Long-term persistent memory is saved to CrateDB.
2. **Semantic Caching:** Frequent or identical Retrieval queries will be cached in Redis to bypass CrateDB and the LLM entirely when appropriate.
3. **Rate Limiting:** Redis will act as the distributed store for API rate-limiting rules to protect the LLM and Database layers from abuse.

## Consequences
### Positive
- **Horizontal Scalability:** API nodes can auto-scale up or down instantly based on traffic.
- **Performance:** Redis provides sub-millisecond latency for conversation state retrieval and query caching.
- **Resilience:** If an API node crashes mid-conversation, the user's next request seamlessly routes to another node without losing context.

### Negative
- Adds a new infrastructural dependency (Redis cluster) to the deployment stack.
