# TrueRAG API

HTTP/API module for request context resolution and controller-based endpoint orchestration.

## Controller Surface

Endpoints are implemented with ASP.NET Core controllers in `TrueRag.Api/Controllers` and composed by `TrueRag.Host`.

## API Layer Structure

`TrueRag.Api` uses the following boundary structure:
- `Controllers/` for HTTP transport endpoints
- `Services/` for API-level orchestration adapters
- `Helpers/` for shared HTTP result/error mapping
- `Middleware/` for correlation, exception normalization, and tenant/app guard
- `Extensions/` for DI and app-pipeline composition
- `Models/` for API-facing response contracts
- `ResourceGuard/` reserved for API-boundary admission components

## API Pipeline Order

Host composes API middleware in this order:
1. `GlobalExceptionMiddleware`
2. `CorrelationIdMiddleware`
3. `ResourceGuardMiddleware`
4. `TenantScopeGuardMiddleware`
5. Controller dispatch

## Search Endpoints
- `POST /api/v1/search/vector`
- `POST /api/v1/search/text`
- `POST /api/v1/search/hybrid`

All search endpoints route through `IRetrievalService` and enforce tenant/app/collection/ACL scope via request context.

Search responses return `RetrievalResponse` with `nodes[]`. Each node includes:
- `nodeId`, `documentId`, `nodeType`, `text`, `score`, `fidelityLevel`
- legacy provenance fields: `pageNumber`, `boundingBox`, `logicalPath`
- canonical provenance contract: `provenance`
  - `pageNumber`
  - `boundingBox` (`page`, `x`, `y`, `w`, `h`) for high-fidelity sources
  - `logicalPath`

## Ingestion Endpoints
- `POST /api/v1/ingest/async`
- `POST /api/v1/ingest/sync`

## Conversation and RAG Endpoints
- `GET /api/v1/context`
- `POST /api/v1/conversations/threads/{threadId}/turns`
- `GET /api/v1/conversations/threads/{threadId}?take={n}`
- `POST /api/v1/conversations/threads/{threadId}/refresh?recentWindow={n}`
- `POST /api/v1/rag/generate`

## Health Endpoints
- `GET /health/live`
- `GET /health/ready`

Health behavior:
- `live` returns `200` when process is running.
- `ready` returns `200` when critical dependencies are ready.
- `ready` returns `503` when any critical dependency is unavailable.
- health routes are anonymous and excluded from tenant/app scope guard requirements.

## Collection Scope Enforcement

- Guarded API routes (`/api/v1/*`) require `tenant_id`, `app_id`, and `collection_id`.
- `collection_id` is resolved from claim/header and validated against configured pattern.
- Optional collection authorization hook (`ICollectionScopeAuthorizer`) can return `403` for disallowed collection access.

This project is a module library used by `TrueRag.Host`, not a standalone entrypoint.
