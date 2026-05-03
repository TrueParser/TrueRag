# TrueRAG

TrueRAG is a `.NET 10` modular monolith for ingestion, storage, retrieval, and conversation-oriented RAG workflows.

The repository is organized as a single deployable host (`TrueRag.Host`) with bounded context modules. It is designed to work with pre-parsed and pre-embedded payloads and to enforce strict tenant, app, and ACL scoping in storage and retrieval paths.

## Purpose and Scope

TrueRAG focuses on pushing beyond standard token-chunked RAG by leveraging structural document metadata. 

**Core Capabilities:**
- **Enterprise-Grade Async Ingestion (WAL + NATS):** Engineered for massive scale, TrueRAG utilizes a node-scoped Custom Write-Ahead Log (WAL) to persist incoming payloads instantly (sequential disk I/O), completely shielding the database from single-row insert fragmentation. Background workers batch these payloads into CrateDB via NATS orchestration, unlocking extreme TPS.
- **Convenience Sync Ingestion:** A rate-limited direct-write path (`/sync`) is also available for low-volume or testing environments.
- **Fidelity-Aware Retrieval:** Automatically detects payload fidelity. Expands structural nodes for high-fidelity documents, or gracefully falls back to adjacent-chunking for standard payloads.
- **General Purpose Search APIs:** Independent endpoints for Vector (kNN), Full-Text (BM25), and Hybrid (RRF) search.
- **Strict Multi-Tenancy:** Hard data isolation using `tenant_id`, `app_id` (namespaces), and Document-Level ACL filtering pushed entirely down to the database layer.

**Advanced RAG Orchestration (The Vision):**
- **Multi-Hop Document Linking:** Traversing interconnected document nodes (e.g., finding the master agreement linked to an addendum) before generation.
- **Version-Aware Structural Diffing:** RAG that can intelligently compare differences between two versions of the same document section.
- **Dual-Layer Confidence Scoring:** Fusing database retrieval scores (RRF) with LLM-derived certainty metrics.
- **Stateless Conversation Memory:** Redis-backed thread memory that uses intelligent summaries to reduce context-window bloat.

*Note: TrueRAG does not implement OCR, parser extraction, token chunking, or embedding generation. Those are upstream concerns and should be handled separately.*

## Architecture

Implementation follows a modular monolith design:

- `TrueRag.Host`: single executable composition root and runtime configuration owner
- `TrueRag.Api`: HTTP boundary and request-context resolution
- `TrueRag.Core`: contracts, domain models, abstractions, primitives
- `TrueRag.Ingestion`: normalization, WAL append/read contracts, queue publication, ingestion execution
- `TrueRag.Storage`: repository implementations and SQL dialect routing
- `TrueRag.Retrieval`: shared retrieval service and fidelity-aware expansion logic
- `TrueRag.Workers`: background queue worker, WAL replay, WAL prune services
- `TrueRag.Conversations`: conversation module scaffold (extended functionality pending)

## Build and Run

Prerequisites:

- `.NET SDK 10`
- PostgreSQL or CrateDB endpoints configured for read/write
- NATS (for async ingestion and workers)
- Docker (optional, for Testcontainers integration tests)

Build:

```bash
dotnet build TrueRag.sln
```

Run host:

```bash
dotnet run --project src/TrueRag.Host/TrueRag.Host.csproj
```

## Configuration

Runtime configuration is owned by `TrueRag.Host`:
- `src/TrueRag.Host/appsettings.json`
- `src/TrueRag.Host/appsettings.Development.json`

Important sections:

- `ConnectionStrings` for read/write data sources
- `RequestContext` for header/claim mapping
- `IngestionRuntime` for `NodeId`, WAL root, WAL durability, sync concurrency
- `Queue` for NATS subject/stream settings
- `IngestionFidelity` for auto-detect/override behavior
- `RetrievalEngine` for high-fidelity requirement and standard fallback

## HTTP Endpoints

Current host maps:

- `POST /api/v1/ingest/async`
- `POST /api/v1/ingest/sync`
- `POST /api/v1/search/vector`
- `POST /api/v1/search/text`
- `POST /api/v1/search/hybrid`
- `GET /api/v1/context`

## Runtime Behavior

- `ingest/async` returns after WAL append and queue publish. Search visibility is eventual and depends on worker replay/batch insert timing.
- `ingest/sync` writes directly to the write database path and is guarded by a concurrency gate.
- `TrueRag.Host` is the only executable. API routes and background workers run in the same process.

## Request Context Requirements

All ingestion and retrieval paths are scoped by request context:

- `tenant_id` is the hard isolation boundary
- `app_id` is the namespace boundary inside a tenant
- `allowed_document_groups` is required for ACL pre-filtering

If ACL groups are empty at retrieval time, results are default-deny.

## Testing

Run unit tests:

```bash
dotnet test tests/TrueRag.UnitTests/TrueRag.UnitTests.csproj
```

Run integration tests:

```bash
dotnet test tests/TrueRag.IntegrationTests/TrueRag.IntegrationTests.csproj
```

Some integration tests use Testcontainers and require Docker.

## Current Delivery Status

As of current repository state:

- foundation/scaffolding is complete
- core ingestion/retrieval implementation is largely in place
- advanced conversation/orchestration phases remain pending
- conversation endpoints and LLM orchestration are not active in the host yet

## Licensing

TrueRAG is dual-licensed.

- AGPL-3.0 for open deployments and modifications
- Commercial licensing for private/proprietary usage

## Security and Contributions

- Security vulnerabilities should be reported privately to the maintainers before public disclosure.
- External code contributions are currently not accepted as this is an internal reference architecture, but bug reports and feature requests are welcome.
