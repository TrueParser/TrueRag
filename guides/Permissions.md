# Data Permissions & Namespace Isolation

This document defines how TrueRAG ensures that users only retrieve content they are explicitly authorized to see. Vector databases are prone to data leaks if semantic search is not strictly bounded by Access Control Lists (ACLs).

## 1. Tenant and Namespace Isolation

TrueRAG operates on a strict hierarchy for data segregation:
* **Tenant (`tenant_id`):** The absolute highest boundary. Data from Tenant A can *never* be queried by Tenant B.
* **Namespace/App (`app_id`):** A logical sub-division within a tenant. A single tenant might have a "HR_Namespace" and an "Engineering_Namespace".

**How it works during Search:**
By default, searches are scoped to a specific Namespace. The `IRequestContext` provides `tenant_id = "TenantA"` and `app_id = "HR_Namespace"`. The Retrieval Engine appends `WHERE tenant_id = 'TenantA' AND app_id = 'HR_Namespace'`.

## 2. Document-Level ACLs (Access Control Lists)

Even within the same Tenant and Namespace, User A might not have permission to view Document A (e.g., a manager's performance review). TrueRAG implements strict **Pre-Filtering ACLs**.

### Step 1: Ingestion (Storing the Permissions)
Yes, permissions **must be ingested alongside the document**. 
When TrueParser (or the upstream system) sends the JSON payload to the TrueRAG `/api/v1/ingest/async` endpoint, the payload must include an array of allowed groups or roles.

*Example Ingestion JSON:*
```json
{
  "document_id": "doc_secret_project",
  "allowed_document_groups": ["engineering_leadership", "executives"],
  "chunks": [ ... ]
}
```
During the WAL background worker bulk-insert, this `allowed_document_groups` array is stored as a native `ARRAY(STRING)` column in CrateDB for every single chunk.

### Step 2: Retrieval (Filtering the Search)
When User A issues a semantic search prompt:
1. The Orchestrator authenticates User A and determines they belong to the `["junior_engineers"]` group.
2. The Orchestrator builds the `IRequestContext` and passes `AllowedDocumentGroups = ["junior_engineers"]` to the Retrieval Engine.
3. The CrateDB Vector Search query is mathematically forced to intersect with these groups using the array overlap operator (`&&`):

```sql
SELECT text, _score 
FROM nodes 
WHERE tenant_id = 'TenantA' 
  AND app_id = 'Engineering_Namespace'
  AND allowed_document_groups && ['junior_engineers'] -- The user's groups
  AND knn_match(vector, [0.1, 0.2...], 10)
```

**Result:** Because `["junior_engineers"]` does not intersect with `["engineering_leadership", "executives"]`, Document A is mathematically excluded from the vector search. User A will never see it, and the LLM will never be fed its text.

## 3. Invariants
* **Pre-Filtering Only:** TrueRAG will *never* do post-filtering (doing a vector search first, and then hiding unauthorized results). Post-filtering breaks search ranking algorithms and pagination. The permissions must be enforced inside the database engine during the `knn_match`.
* **Zero-Trust Default:** If a document is ingested without an `allowed_document_groups` array, it must default to a restricted state (e.g., accessible only by tenant admins), unless explicitly marked as `["public"]`.
