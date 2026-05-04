# ADR 015: Observability Metrics with Prometheus and Grafana

## Status
Accepted

## Context
TrueRAG requires production-grade observability for ingestion, WAL durability, retrieval quality, and conversation/LLM orchestration.  
Phase 5 defines a metrics-first implementation that will be scraped by Prometheus and visualized in Grafana.

The architecture must preserve:
- modular monolith boundaries (ADR 003)
- node-safety and admission signals (ADR 013)
- fidelity-aware retrieval behaviors (ADR 006)
- stateless API constraints (ADR 008)

## Decision
1. **Metrics backend and visualization**
- Prometheus is the canonical metrics collector.
- Grafana is the canonical visualization and alerting dashboard.
- `TrueRag.Host` exposes a scrape endpoint (`/metrics`) for process-level and app-level metrics.
- Metrics are exposed on a **dedicated listener** (separate port from public API) with configurable local bind IP.
- The dedicated metrics listener is designed to remain outside JWT-protected API routes and be reachable only from allowed network paths (for example local/VPC/private subnet access).

2. **Metric naming and units**
- Metric names use `truerag_*` prefix.
- Durations use `_seconds` suffix with histogram/counter conventions.
- Totals use `_total`; point-in-time values use gauges.

3. **Label cardinality rules**
- Low-cardinality labels are allowed (e.g., `endpoint_family`, `mode`, `node_state`, `result`).
- High-cardinality labels are forbidden (e.g., `document_id`, raw `thread_id`, raw query text, user identifiers).
- Tenant/app labels must be avoided unless explicitly bucketed/anonymized.

4. **Histogram bucket policy**
- Shared bucket profiles are defined per metric family (ingest, retrieval, generation, storage).
- Buckets are standardized across modules for comparable dashboards and SLOs.

5. **Required observability surfaces**
- Ingestion + WAL metrics.
- Worker + storage visibility metrics.
- Search + fidelity + ACL metrics.
- Conversation + LLM generation metrics.
- Node-state/admission metrics for overload behavior.

6. **Operational deliverables**
- Grafana dashboards for:
  - ingest/WAL health
  - search and retrieval performance
  - conversation/generation performance and failure rates
  - admission/node-state transitions
- Alerting rules for:
  - sustained overload
  - WAL lag growth
  - retrieval/generation error spikes
  - health probe failures
- Dedicated deployment configuration for metrics bind host/port and network allowlisting.

7. **Documentation contract**
- A metrics catalog is maintained with:
  - metric name
  - type (counter/gauge/histogram)
  - unit
  - labels
  - owning module
  - dashboard/alert references

## Consequences
### Positive
- Consistent, queryable telemetry across all core modules.
- Faster incident diagnosis through standardized metrics and dashboards.
- Safer scaling and rollback decisions with explicit overload/lag signals.

### Negative
- Additional implementation and maintenance overhead.
- Risk of noisy or expensive time series if label discipline is not enforced.
- Requires ongoing dashboard and alert tuning per environment.
- Requires careful deployment networking to avoid exposing unauthenticated metrics endpoints publicly.
