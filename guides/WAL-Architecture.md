# WAL Architecture (TrueRAG Async Ingestion)

This document summarizes the Write-Ahead Log (WAL) architecture utilized for asynchronous ingestion in the TrueRAG API. This implementation directly reuses the highly-optimized, lane-sharded WAL developed for TrueParser.

## Scope

- Ingestion acceptance and durability boundary
- WAL record append/read/replay flow
- Worker payload-open contract
- Recovery and completion marker behavior

## High-Level Flow

1. API receives ingestion payload (TrueParser JSON + Embeddings) via direct or presigned ingestion routes.
2. Payload is appended to node-local WAL through `IIngestionAcceptanceLog.AppendAsync(...)`.
3. Ingestion job is published to NATS with persisted WAL coordinates:
   - `walPath`
   - `walSegmentId`
   - `walOffset`
   - `walLength`
4. Background Worker opens payload through `IIngestionWalReader.OpenPayloadAsync(...)`.
5. Worker batches the payloads, performs **Bulk Inserts** to the CrateDB Central Publisher, and publishes final completion.
6. Final stage stores the parsed output and writes the WAL completion marker.
7. Replay/prune workers use checkpoints + completion markers for safe recovery.

## Ingestion Paths

*(Note: TrueRAG also provides a `/api/v1/ingest/sync` endpoint for low-volume convenience, as detailed in ADR 007. However, that endpoint bypasses the WAL entirely. The paths below represent the Enterprise WAL-Buffered routes.)*

### Direct Ingestion
- `/api/v1/ingest/async`
- Request body is accepted, appended to WAL, and queued for database bulk insertion.

### Presigned Ingestion
- `/api/v1/ingest/request-url`
- `/api/v1/ingest/complete`
- Presigned completion leads to a staging flow that appends the downloaded data to the WAL, then queues the database bulk insert job.

## Core Components

### WAL Append
- **Interface:** `IIngestionAcceptanceLog`
- **Responsibilities:**
  - lane-sharded append queueing
  - tenant-aware throttling and queue-depth based rejection (load shedding)
  - durability-framed WAL record persistence (with optional `fsync`)
  - returning WAL coordinates for downstream worker queueing

### Sharding, Lanes, and Segment Topology
- Shard index is computed per ingestion key and mapped into the configured shard count.
- Lane key format is tenant/app/shard scoped: `{tenantId}:{appId}:shard-{shardIndex}`
- Each lane owns a segment sequence under the lane directory: `segment-{sequence:D20}.wal`
- Active segment metadata is persisted so restart can continue appending to the correct segment.
- **WAL routing invariants:**
  - No cross-lane reordering
  - Lane is the append/replay ordering boundary
  - Node-local lane ownership is strictly preserved

### Hot Segment Rotation and Retirement
- Writer rotates the active segment when hot-lane conditions require it (including max lane byte policy).
- Rotation advances the lane state to the next `segment-{n}.wal`.
- A retirement marker (`.retired`) is used as a lifecycle gate before physical deletion.
- Active-lane retirement can be triggered by the prune service on the cold path: if a lane is idle and all records are completed, the active segment is first retired, then deleted on a later prune tick.

### WAL Replay Checkpoint Semantics
- Replay reads the lane checkpoint from `*.checkpoint` and resumes from the last durable offset.
- Checkpoint is advanced independently of worker execution after successful republish attempt/ack path.
- Replay handles truncated tails by persisting a safe checkpoint without truncating the active writer tail.
- Replay publishes jobs using WAL coordinates, ensuring identical behavior to the hot path.

### WAL Pruning Model
- **Service:** `IngestionWalPruneService`
- Prune runs periodically and evaluates lanes conservatively.
- Practical prune gating relies on three criteria:
  1. **Completion Coverage:** Every WAL record in the lane has a matching `.completed.{offset}` marker.
  2. **No Active Read Lease:** Lane is not currently opened by a worker WAL reader.
  3. **Lane is Cold/Retired:** Lane is idle past the retire window and is not an active writer lane (or is already marked retired).
- **Safety Guards:** Deletion tolerates transient IO races. Marker/checkpoint cleanup happens strictly *after* successful lane-file delete.

### WAL Reader
- **Interface:** `IIngestionWalReader`
- **Responsibilities:**
  - node ownership and lane-path validation
  - WAL framing/version validation
  - payload stream reconstruction/open
  - integrity checks (record/chunk/checksum)

### WAL Replay
- **Service:** `IngestionWalReplayService`
- **Responsibilities:**
  - scan WAL lanes from persisted checkpoint offset
  - republish failed/dropped ingestion jobs from WAL coordinates
  - persist checkpoint advancement
  - tolerate duplicates via idempotent publish behavior

## Invariants

- WAL acceptance + queue publish is the async processing acceptance boundary.
- Replay must publish using persisted WAL coordinates, not inferred payload state.
- Completion markers are written only after terminal CrateDB bulk insert completion.
- Replay/prune safety depends on parity across:
  1. append metadata
  2. replay checkpoint progression
  3. completion marker writes
- Do not add new Redis request hot-path calls without explicit scope approval.

## Operational Signals

- **WAL append metrics:**
  - `truerag_wal_append_attempts_total`
  - `truerag_wal_append_success_total`
  - `truerag_wal_append_failures_total`
  - `truerag_wal_append_duration_ms`
- **WAL replay metrics:**
  - `truerag_wal_replay_duration_ms`
  - `truerag_wal_recovery_failures_total`
