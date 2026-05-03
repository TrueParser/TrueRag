# ADR 007: Asynchronous Ingestion via Custom WAL

## Status
Proposed

## Context
Ingesting high-fidelity JSON from TrueParser, including text, structure, and dense vector embeddings, is an I/O intensive process. While CrateDB handles massive throughput well, hammering the database with single-document inserts can cause index fragmentation (Lucene segment merging) and network overhead. 

The team will reuse a highly optimized, battle-tested Custom WAL (Write-Ahead Log) implementation from the TrueParser project that supports shards, lanes, retirement, and cold-data pruning.

## Decision
We will implement a **Dual-Path Ingestion Pipeline** to support both Enterprise (high-TPS) and Small User (convenience) workloads.

1. **Enterprise Async Path (`/api/v1/ingest/async`):** 
   The API will serialize the incoming payload and append it directly to the local/node-scoped WAL. Because WAL writes are sequential disk I/O, the API response time will be near zero.
   * **Payload Content:** The incoming payload will contain both the text and the pre-computed vector embeddings.
   * **Optional `fsync` Durability:** Toggle for `fsync` guarantees physical disk flush before returning `202 Accepted` (max durability) or OS-memory buffer flush (max TPS).

2. **Convenience Sync Path (`/api/v1/ingest/sync`):**
   A synchronous endpoint that parses the payload and executes a direct `INSERT` against the CrateDB Central Publisher, blocking until the transaction commits and returning `200 OK`. 
   > [!WARNING]
   > **Known Limitations of Sync Ingestion:**
   > - **Connection Exhaustion:** Spikes in traffic will exhaust the API thread pool and DB connection pool.
   > - **Database Fragmentation:** Bypasses worker bulk-inserts, causing high CPU load and Lucene segment fragmentation due to single-row inserts.
   > - **Coupled Uptime:** If CrateDB is restarting or unreachable, this endpoint will fail with a 500 error.

3. **Local Worker Nodes:** 
   Background workers will continuously tail the WAL for the async path, pulling events, batching them in memory, and executing **Bulk Inserts** into CrateDB. 

## Consequences
### Positive
- **Massive TPS Increase:** Sequential disk I/O with optional `fsync` allows tuning exactly for the desired balance of throughput vs. safety on the async path.
- **Reduced CrateDB Load:** The worker batches the WAL records. Bulk inserting Text + Vectors simultaneously drastically reduces CrateDB's CPU load and Lucene segment merging overhead.
- **Resiliency:** If CrateDB goes offline, the async API safely buffers the massive vector payloads to disk until the database recovers.
- **Unified Payload Handling:** Despite maintaining two distinct ingestion flows (Enterprise Async vs Convenience Sync), both paths strictly consume the identical TrueRAG Ingestion Contract DTO, keeping validation and schema mapping DRY.

### Negative
- Requires disk space monitoring on the API nodes to ensure the WAL doesn't fill up the local storage if the database is down for an extended period.
- **Eventual Consistency (Async Path):** The primary WAL-based ingestion path is eventually consistent, meaning a document uploaded to `/async` won't be instantly searchable. *(Note: The `/sync` path exists to provide immediate consistency for low-volume workloads, but it completely bypasses the WAL pipeline and must be strictly rate-limited).*
