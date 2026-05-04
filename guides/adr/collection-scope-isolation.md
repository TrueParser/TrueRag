# ADR 020: Collection Scope as First-Class Isolation Boundary

## Status
Proposed

## Context
TrueRAG currently enforces isolation primarily with `tenant_id` and `app_id`, plus document ACL pre-filtering:

- `IRequestContext` contains `TenantId`, `AppId`, and `AllowedDocumentGroups` (no `CollectionId`).
- API request-context resolution requires tenant/app from claim or header.
- Storage predicates enforce `tenant_id = @tenant_id AND app_id = @app_id` and ACL overlap.
- Storage guard validation requires non-empty tenant/app only.
- Conversation state/cache keys are scoped by tenant/app/thread.

This behavior is visible in:
- `src/TrueRag.Core/Context/IRequestContext.cs`
- `src/TrueRag.Api/Context/HttpRequestContextResolver.cs`
- `src/TrueRag.Storage/Persistence/StorageSqlDialect.cs`
- `src/TrueRag.Storage/Persistence/StorageGuard.cs`
- `src/TrueRag.Storage/Persistence/SqlParameterBinder.cs`
- `src/TrueRag.Conversations/DistributedConversationStateStore.cs`
- `src/TrueRag.Retrieval/DistributedRetrievalSemanticCache.cs`

This model is valid for tenant/app isolation, but it does not provide first-class multi-collection partitioning inside an app (for example one billing app serving multiple departments).

## Decision
Add `collection_id` as a first-class required scope for guarded API routes and persistence/query predicates.

Target scope hierarchy:
1. `tenant_id` (hard boundary)
2. `app_id` (application/billing boundary)
3. `collection_id` (department/workspace boundary)
4. `allowed_document_groups` (document-level ACL boundary)

Rules:
- Every guarded request under `/api/v1/*` must resolve `collection_id` (claim and/or header policy).
- All ingest/retrieval/conversation/storage paths must enforce `tenant_id + app_id + collection_id`.
- ACL filtering remains pre-filtered inside SQL and is evaluated within collection scope.
- Missing/invalid collection scope is default-deny.
- Existing health/readiness routes stay outside tenant/app/collection enforcement.

## Consequences

Positive:
- Enables multiple collections under one app without weakening isolation.
- Keeps app-level billing semantics while adding department-level partitioning.
- Reduces accidental cross-department leakage risk.

Trade-offs:
- Requires schema and contract updates across modules.
- Requires upstream clients/control-plane integration to send collection per request.
- Requires migration/backward-compatibility rollout strategy for existing app-scoped data.

## Upstream Integration Pattern
- Keep token identity claims stable (`tenant_id`, `app_id`, identity/roles).
- Pass `collection_id` per request (header/body), validated server-side against policy.
- Do not encode a single fixed collection in long-lived app tokens when one app serves many collections.

## Required Follow-Up Work
- Execute Phase 9 tasks in `TASK.md` before embedding-scope expansion in Phase 8.
- Update Permissions and Invariants docs to include collection scope as mandatory for guarded paths.
