# TrueRAG Ingestion Module

## Purpose
`TrueRag.Ingestion` owns canonical request normalization and ingestion execution paths.

Current scope:
- canonical contract mapping from `IngestionRequestDto`
- payload validation and fidelity detection
- async buffered ingest via WAL append + node-scoped queue publish
- sync direct ingest with bounded concurrency

## Public API Surface

### DI Registration
- `IngestionModule.AddTrueRagIngestion(IServiceCollection)`

### Contracts and Services
- `IIngestionNormalizer`
- `IIngestionExecutionService`
- `IIngestionAcceptanceLog`
- `IIngestionWalReader`
- `IQueuePublisher`
- `IQueueSubscriber`

### Endpoints (wired in Host)
- `POST /api/v1/ingest/async`
- `POST /api/v1/ingest/sync`

## Configuration
- `IngestionRuntime`
: `NodeId`, `WalRootPath`, `SyncMaxConcurrency`
- `IngestionFidelity`
: `DefaultMode` (`auto`/`high`/`standard`), `AllowExplicitOverride`
- `Queue`
: `Url`, `StreamName`, `SubjectPrefix`

## Queue and WAL Behavior
- Async accept path appends payload to WAL and captures coordinates.
- Async job is published to node-scoped subject: `TrueRAG.Job.Ingest.<nodeId>`.
- Queue transport uses NATS JetStream with stream provisioning warmup.
- Worker consumes queue message, rehydrates payload from WAL coordinates, persists to storage, then marks completion.

## Responsibilities
- Keep parser-facing contract mapping isolated from storage/retrieval logic.
- Keep async and sync execution on the same request contract.
- Preserve tenant/app/document scope metadata through queue and WAL boundaries.
- Enforce zero-trust ACL ingestion: `AllowedDocumentGroups` must contain at least one non-empty group value.
