# NATS Job Architecture (TrueRAG)

This document summarizes the NATS JetStream job orchestration architecture used in TrueRAG. This architecture works in tandem with the local node-scoped WAL to guarantee data locality.

## The Locality Problem
Because the `IIngestionAcceptanceLog` writes the ingestion payload to a local WAL file on the specific API node that received the HTTP request, any background worker that processes that job *must* be running on that exact same node. If Worker B tries to process a job whose WAL file is on Node A's local disk, it will fail.

## Node-Scoped Subjects and Consumers
To solve this, TrueRAG utilizes **Node-Scoped NATS Subjects**.

1. **Job Pipeline:** `TrueRAG.Job.Ingest.*`
2. **Final-Insert Pipeline:** `TrueRAG.Insert.*`

### How Jobs Are Node-Scoped
1. Base topic is resolved (e.g., `TrueRAG.Job.Ingest`).
2. Node scope is appended using the node's unique identifier.
3. Final subject becomes `TrueRAG.Job.Ingest.<nodeId>`.

When the API appends data to the WAL, it publishes the WAL coordinates (Path, SegmentId, Offset) to this specific node-scoped NATS subject.

### How Workers Are Node-Scoped
Each TrueRAG Ingestion Worker binds strictly to its local node's topic and consumer group:
- **Topic pattern:** `TrueRAG.Job.Ingest.<nodeId>`
- **Consumer group pattern:** `ingest-group-<nodeId>`

Because of this suffixing, Node A's workers only ever receive NATS messages intended for Node A.

### Why This Design is Crucial
1. `NodeId` is mandatory at startup.
2. WAL paths and reads are inherently node-bound. The `IIngestionWalReader` enforces node ownership so job-to-worker locality is mathematically preserved.
3. Subject and consumer-group suffixing guarantees each node consumes only its own lane of work, eliminating cross-node network chatter for WAL payloads.
4. The WAL Replay service republishes to that exact same node scope, keeping recovery behavior identical to the hot path.
