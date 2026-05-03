# ADR 012: Auth-Agnostic Core & Logical Multi-Tenancy

## Status
Proposed

## Context
As TrueRAG is designed as an Enterprise Modular Monolith (and potentially a reusable framework), hardcoding specific authentication mechanisms (like JWT, OIDC, or API Keys) deep into the core logic would couple the system to a specific hosting model. Additionally, the system must securely isolate data across multiple tenants and namespaces.

## Decision

We will implement an **Auth-Agnostic Core with Logical Multi-Tenancy**.

### 1. The Orchestrator Boundary
* **Host + API Module (`TrueRag.Host` / `TrueRag.Api`):** The executable host owns authentication and authorization setup, while the API module handles route mapping and request validation. Together they form the top-level orchestrator boundary.
* **Context Building:** Upon successful authentication, the orchestrator extracts the identity and builds an `IRequestContext` (containing `TenantId`, `AppId`/`Namespace`, and `Roles`).
* **Dependency Injection:** The `IRequestContext` is registered in the DI container as a Scoped service.
* **Core & Storage Layers (`TrueRag.Core`, `TrueRag.Storage`):** These layers know nothing about HTTP or tokens. They simply inject the `IRequestContext` and execute business logic and database queries using both the provided `TenantId` and `AppId` as first-class isolation boundaries.

### 2. Logical Isolation in CrateDB (Pooled Multi-Tenancy)
Instead of provisioning separate databases or clusters per tenant, TrueRAG will utilize **Logical Multi-Tenancy** within a single shared CrateDB cluster.

* **Schema Design:** Every table in CrateDB (e.g., `nodes`, `conversations`) will include `tenant_id` and `app_id` columns.
* **CrateDB Routing Columns:** CrateDB supports [Routing Columns](https://cratedb.com/docs/crate/reference/en/latest/general/ddl/sharding.html#routing) to determine which shard stores a row. We will use `tenant_id` as the routing column. This guarantees that all data for a specific tenant lives on the same shard, making tenant-scoped queries lightning fast.
* **Strict Enforcement:** The `ICrateOrPgRepository` implementation must strictly append `WHERE tenant_id = @tenant_id AND app_id = @app_id` (extracted from the `IRequestContext`) to *every single* `SELECT`, `INSERT`, `UPDATE`, and `knn_match` query. 

### 3. The Permission Boundary (Action vs. Data)
Permissions will be strictly bifurcated between the Orchestrator and the Core to maintain separation of concerns:
* **Action Permissions (Orchestrator):** The host/API boundary determines *if* the user can invoke an action (e.g., `[Authorize(Policy = "CanIngest")]`). The core library inherently trusts that if a method was called, the caller is authorized to execute the action.
* **Data Permissions / ACLs (Core):** Document-level visibility is baked into the Core. The `IRequestContext` will carry a list of allowed scopes/groups, ensuring the CrateDB vector search only retrieves documents the user is authorized to see.

**The Boundary Contract:**
```csharp
public interface IRequestContext
{
    // 1. Hard Multi-Tenancy (Baked into every single SQL query)
    string TenantId { get; }
    
    // 2. Application/Namespace Isolation (Optional separation within a tenant)
    string AppId { get; }

    // 3. Data-Level Permissions (Used by Retrieval Engine to filter Vector Searches)
    // Example: ["engineering_docs", "public_docs"]
    IReadOnlyCollection<string> AllowedDocumentGroups { get; }
}
```

## Consequences

### Positive
- **Extreme Flexibility:** The TrueRAG framework can be dropped into an internal corporate network (using Windows Auth) or a public SaaS platform (using JWTs/Stripe) without modifying a single line of core retrieval code.
- **High Performance:** Using CrateDB's `tenant_id` routing columns ensures that even with billions of vectors in the table, queries instantly prune irrelevant shards and only scan the nodes belonging to the requesting tenant.
- **WAL Alignment:** This exactly mirrors the WAL's `{tenantId}:{appId}:shard-{shardIndex}` lane structure.

### Negative
- **Data Spill Risk:** If a developer accidentally forgets to append `WHERE tenant_id = @tenantId` to a raw SQL query in the repository, data leaks across tenants. This requires strict code-review invariants or Repository-level abstractions to force the appending of the tenant clause.
