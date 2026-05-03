# TrueRAG System Invariants

This document defines the absolute, non-negotiable architectural invariants for the TrueRAG system. **No code or feature shall be merged if it violates these invariants.**

## 1. Redis Hot-Path Restrictions
* **No Redis in the Ingestion Hot Path:** When a document is ingested via `/api/v1/ingest/async`, the API must never make a Redis network call for distributed locking, tenant coordination, or deduplication. The hot path must be strictly sequential I/O to the local WAL.
* **Strict Cache/State Boundary:** Redis is permitted in the hot path *only* for the Retrieval/Conversation API to handle caching (Semantic Cache), Rate Limiting, and ephemeral Conversation Thread State. It is not an operational database.

## 2. WAL (Write-Ahead Log) Invariants
* **Strict Node Locality:** A WAL file written on Node A can only be read, processed, and pruned by background workers running on Node A.
* **Acceptance Boundary (Async):** The `/api/v1/ingest/async` endpoint must not return `202 Accepted` until the NATS job is published *and* the payload is successfully flushed to the WAL. (Note: "flushed" means physical disk write if `fsync=true`, or OS-memory buffer flush if `fsync=false`).
* **Synchronous Hot-Path Protection:** Because the `/api/v1/ingest/sync` endpoint bypasses the WAL and hits CrateDB directly, it **must** be protected by a strict rate-limiter (e.g., max concurrent connections) to prevent database connection pool exhaustion and Thundering Herd attacks.
* **Coordinate-Based Replay:** Any replay mechanism or recovery worker must publish jobs using persisted WAL coordinates (`walPath`, `Offset`, `SegmentId`), never inferred payload states.
* **No Cross-Lane Reordering:** Within a tenant/app/shard lane, messages must be appended and processed strictly in order.

## 3. CrateDB Architecture Invariants
* **CQRS Routing:** 
  * All `INSERT`/`UPDATE`/`DELETE` operations (Ingestion Module) must be routed to the **Central Sharded Publisher Cluster**.
  * All `SELECT`/`knn_match`/`MATCH` operations (Retrieval Module) must be routed to the nearest **Global Edge Subscriber Cluster**.
* **Stateless Retrieval:** The Retrieval Engine must not hold in-memory application state regarding document contexts. All context state must be fetched dynamically from Redis or CrateDB per request.

## 4. Feature Boundaries
* **No Upstream Parsing Logic:** The API shall never perform document OCR, text splitting/chunking, or embedding generation. It strictly accepts pre-parsed, pre-embedded JSON payloads that conform to the TrueRAG Ingestion Contract (supporting both High-Fidelity and Standard-Fidelity structures).
* **Graceful Degradation:** If an ingested document lacks high-fidelity metadata (e.g., no bounding boxes or hierarchical tree IDs), the system must degrade to standard adjacent-chunk processing and must not crash.
