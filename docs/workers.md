# TrueRAG Workers

`TrueRag.Workers` provides background ingestion execution hosted inside `TrueRag.Host`.

## Responsibilities
- Subscribe to node-scoped ingestion queue subjects (`TrueRAG.Job.Ingest.<nodeId>`).
- Read buffered payloads from WAL by coordinates.
- Replay uncommitted WAL records on startup sweep and republish ingestion jobs.
- Persist rehydrated payloads through `IIngestionRepository`.
- Write completion markers and run WAL prune/retirement service.

## Hosted Services
- `IngestionQueueWorker`
- `IngestionWalReplayService`
- `IngestionWalPruneService`

This project is a module library and does not own process startup.
