# ADR 013: Node-Safety Admission and Backpressure Control

## Status
Proposed

## Context
TrueRAG must remain stable under ingestion and retrieval pressure while preserving the modular monolith model and WAL invariants.

The ingestion path already relies on node-local WAL durability and node-scoped queue processing. Introducing external distributed dependencies in ingestion admission would weaken hot-path reliability and violate existing design intent around local sequential WAL acceptance.

The system needs explicit node-safety controls for:

1. request admission limits under CPU/memory/threadpool pressure
2. WAL queue pressure and shard/lane saturation
3. controlled overload shedding with predictable retry behavior

## Decision
TrueRAG will implement a **Node-Safety Admission and Backpressure subsystem** with the following constraints:

1. **Node-Local First (Ingestion)**
   - Ingestion admission for `/api/v1/ingest/async` remains node-local.
   - No Redis dependency is introduced in ingestion hot-path admission checks.
   - Admission is based on lane/family depth, WAL pressure, and local resource pressure.

2. **Lane/Family Admission Gates**
   - Admission keys are scoped by `tenant_id` + `app_id` and lane/family identity.
   - Queue depth reservation/release lifecycle is explicit to avoid over-admission.
   - Rejections return explicit overload/backpressure reasons.

3. **WAL Pressure Model**
   - The host tracks WAL pressure snapshots using document-size-agnostic signals:
     - max shard queue usage
     - average shard queue usage
     - drain capacity ratio
     - queue rejection rate
     - faulted shard count
   - These signals feed degraded/overloaded state transitions.

4. **Host Resource Monitor + Hysteresis**
   - Node state is computed from CPU, memory, threadpool pressure, active requests, and WAL pressure.
   - State transitions use hysteresis to prevent oscillation.
   - Overloaded state enables controlled request shedding.

5. **Resource Guard Middleware**
   - Protected routes can be rejected with retry hints when node state is overloaded.
   - Health and control endpoints may be bypassed by configuration.

6. **Retrieval/Conversation Limits**
   - Retrieval and conversation endpoint safety limits are node-local by default.
   - Global business quotas remain an upstream concern unless a future ADR introduces distributed quota enforcement.

## Consequences
### Positive
- Preserves ingestion hot-path reliability and local WAL guarantees.
- Prevents node collapse under burst traffic.
- Keeps behavior deterministic and observable with explicit rejection causes.
- Aligns with modular boundaries and host-owned runtime policy.

### Negative
- Per-node limits are not global plan/quota enforcement.
- Requires careful tuning of thresholds per deployment shape.
- Adds operational complexity in admission and overload configuration.
