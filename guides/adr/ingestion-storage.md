# ADR 004: Ingestion, Storage, and Replication Layer

## Status
Proposed

## Context
TrueRAG needs to handle high-fidelity structural document data from TrueParser. The storage layer must provide global low latency for search queries while ensuring data consistency during ingestion. Additionally, the system must maintain compatibility with both **CrateDB** and standard **PostgreSQL**.

## Decision
We will use a **Centralized Ingestion, Distributed Edge-Read** architecture, powered by Logical Replication and a Sharded Primary Cluster.

### 1. Database Compatibility (CrateDB & PostgreSQL)
Since CrateDB uses the PostgreSQL wire protocol, our .NET `TrueRag.Storage` module will use the `Npgsql` ADO.NET driver. 
To support *both* databases seamlessly, we will use the **Repository/Strategy Pattern**:
* **`ICrateOrPgRepository` Interface:** Defines the contract for ingestion and retrieval.
* **`CrateDbRepository`:** Generates CrateDB-specific SQL (e.g., `knn_match` for vectors, `MATCH()` for full-text search, native JSON objects).
* **`PostgresRepository`:** Generates PostgreSQL-specific SQL (e.g., `pgvector` operators `<->`, `to_tsvector` for full-text search, `jsonb` types).

### 2. Global Replication Strategy (Publish/Subscribe)
Both CrateDB and PostgreSQL support the same foundational **Logical Replication (Publish-Subscribe)** model.

* **Central Cluster (The Publisher):** 
  * A horizontally scalable, sharded cluster dedicated to heavy ingestion workloads. 
  * Tables are automatically sharded and write data is distributed across nodes for maximum write throughput. 
  * This cluster hosts the `PUBLICATION`.
* **Global Clusters (The Subscribers):** 
  * Geographically distributed clusters that contain a complete, read-only copy of all data from the central cluster.
  * These clusters use `SUBSCRIPTION` to pull changes (INSERT, UPDATE, DELETE) natively via `ShardReplicationChangesTracker`.
* **Application-Level Routing (CQRS Pattern in .NET):** 
  * The TrueRAG API will implement connection routing. The `Ingestion Module` will open `NpgsqlConnections` strictly to the Central Publisher. 
  * The `Retrieval Module` will open `NpgsqlConnections` strictly to the geographically closest Global Subscriber cluster.

## Consequences
### Positive
- **Write Scalability:** The central cluster scales horizontally for massive ingestion throughput without impacting search performance.
- **Low Latency Reads:** End-users querying the RAG API globally will hit local read replicas, drastically reducing latency for Hybrid Search.
- **High Availability:** If one subscriber cluster fails, application-level routing can failover to another global cluster.
- **Unified Replication Architecture:** Because both DBs use the Publication/Subscription model, this infrastructure works regardless of whether the engine is CrateDB or PostgreSQL.

### Negative
- **Application Complexity:** The `TrueRag.Storage` module must manage two distinct Connection Strings (one for Writes, one for Reads).
- **Replication Lag (Eventual Consistency):** A document ingested centrally might not be instantly available for global search. The API must handle this expectation.
