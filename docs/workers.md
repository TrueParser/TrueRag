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

## Admission and Backpressure Interaction

Workers are the drain side of node-local ingestion pressure and directly influence host admission decisions.

How pressure feeds admission:
- `IngestionPressureTracker` snapshots (accept/drain rates, live queue depth, drain-capacity ratio) are consumed by API `ResourceMonitor`.
- If pressure crosses configured overload thresholds, API resource guard starts rejecting guarded API routes with `429` and `Retry-After`.
- As workers recover drain rate and reduce live depth, node state transitions through `Degraded` and then `Healthy` based on hysteresis settings.

Operational limits to monitor:
- Sustained high `TotalLiveDepth` indicates ingest backlog growth.
- `DrainCapacityRatio > 1.0` means acceptance outpaces drain; sustained growth near overload threshold is a pre-rejection warning.
- WAL replay backlog during startup should converge; non-converging replay indicates storage or worker throughput bottlenecks.

Coordination guidance:
- Tune worker batch and poll settings together with host `ResourceGuard` thresholds.
- Keep node-scoped NATS subjects/consumers aligned with local WAL ownership to avoid cross-node drain failures.
