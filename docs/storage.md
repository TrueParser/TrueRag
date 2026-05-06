# TrueRAG Storage Module

## Purpose
`TrueRag.Storage` implements persistence and query execution behind `TrueRag.Core` repository interfaces.
It is responsible for:
- write-path ingestion persistence
- read-path retrieval query execution
- mandatory tenant/app/collection/ACL data-scoping predicates
- CrateDB and PostgreSQL SQL dialect compatibility

## Public API Surface

### DI Registration
- `StorageModule.AddTrueRagStorage(...)`
  - Registers:
    - `IIngestionRepository` -> `IngestionRepository`
    - `IRetrievalRepository` -> `RetrievalRepository`
    - `IStorageHealthProbe` -> `StorageHealthProbe`
  - Requires:
    - write connection string
    - read connection string
  - Supports independent engine selection for read/write:
    - `DatabaseEngine.CrateDb`
    - `DatabaseEngine.PostgreSql`

### Core Interfaces Implemented
- `TrueRag.Core.Abstractions.IIngestionRepository`
- `TrueRag.Core.Abstractions.IRetrievalRepository`
- `TrueRag.Core.Abstractions.IStorageHealthProbe`

## Read/Write Routing (CQRS-aligned)
- **Writes** always use `StorageDataSources.Write`:
  - `IngestionRepository.UpsertDocumentAsync(...)`
- **Reads** always use `StorageDataSources.Read`:
  - `RetrievalRepository.QueryVectorAsync(...)`
  - `RetrievalRepository.QueryTextAsync(...)`
  - `RetrievalRepository.QueryHybridAsync(...)`

This preserves the architectural invariant that ingestion writes and retrieval reads use separate data paths.

## Predicate Enforcement

All retrieval SQL includes a shared predicate:
- `tenant_id = @tenant_id`
- `app_id = @app_id`
- `collection_id = @collection_id`
- ACL overlap filter:
  - `allowed_document_groups && @acl_groups`

Guardrails:
- `StorageGuard.EnsureScopedContext(...)` rejects missing `TenantId`/`AppId`/`CollectionId`.
- Default-deny ACL binding: when caller has no ACL groups, `@acl_groups` is bound as an empty array (not `NULL`) so overlap evaluates false.
- Predicates are applied inside SQL generation, not left to callers.
- Ingestion writes reject missing/empty `AllowedDocumentGroups` to prevent unscoped documents.

## SQL Dialect Strategy

`StorageSqlDialect` selects SQL per engine:
- **CrateDB**
  - vector: `knn_match(...)`
  - text: `MATCH(...)`
  - hybrid: SQL-side RRF fusion
- **PostgreSQL**
  - vector: `vector <=> @query_vector` (score derived as `1 - distance`)
  - text: `ts_rank_cd(...)` + `websearch_to_tsquery(...)`
  - hybrid: SQL-side RRF fusion

## Current Data Contract Assumptions
- Primary table name: `nodes`
- Expected columns include:
- identifiers and tenancy: `id`, `document_id`, `tenant_id`, `app_id`
- collection boundary: `collection_id`
  - ACL: `allowed_document_groups`
  - retrieval fields: `text`, `vector`, `logical_path`
  - ingestion metadata: `document_group_id`, `version_number`, `parent_id`, `referenced_node_ids`
  - provenance fields: `page`, `x`, `y`, `w`, `h`

## Health and Operations
- `StorageHealthProbe` opens read/write connections to validate availability.
- Errors are returned as `Result` / `Result<T>` with module-specific error codes.

## Schema Migration and Bootstrap

Storage registers `ISchemaMigrationService` with engine-aware migration artifacts and deterministic planning:
- migration history table: `schema_migrations` (`version`, `checksum`, `applied_utc`)
- deterministic ordering by version
- append-only pending selection
- checksum drift validation
- guarded/idempotent DDL enforcement (`IF NOT EXISTS` and non-destructive defaults)

Engine compatibility:
- migration catalog branches by `DatabaseEngine` (`CrateDb`/`PostgreSql`)
- core schema coverage includes:
  - ingestion/retrieval table: `nodes`
  - conversation tables: `conversation_messages`, `conversation_thread_states`
  - embedding profile tables used by storage persistence

Operational contract:
- migrations are forward-only by default
- destructive rewrites are not executed by default migration pipeline
- host command workflow (`migrate status|up|validate`) is the operator control plane
