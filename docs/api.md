# TrueRAG API

HTTP/API module for request context resolution and controller-based endpoint orchestration.

## Controller Surface

Endpoints are implemented with ASP.NET Core controllers in `TrueRag.Api/Controllers` and composed by `TrueRag.Host`.

## Search Endpoints
- `POST /api/v1/search/vector`
- `POST /api/v1/search/text`
- `POST /api/v1/search/hybrid`

All search endpoints route through `IRetrievalService` and enforce tenant/app/ACL scope via request context.

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

This project is a module library used by `TrueRag.Host`, not a standalone entrypoint.
