# Ingestion Contract & DTO Boundaries

This document formalizes the canonical JSON request payload expected from upstream engines (like TrueParser) and the resulting internal `.NET` Data Transfer Objects (DTOs). 

This contract aggregates all architectural decisions regarding fidelity auto-detection, multi-hop linking, and data permissions.

## 1. The Canonical JSON Request Contract

When `POST /api/v1/ingest/async` (or `/sync`) is invoked, the body must adhere to this structure:

```json
{
  "document_id": "doc_99x_alpha",
  "document_group_id": "MSA_Contracts",
  "version_number": "1.0",
  "allowed_document_groups": ["legal_team", "executives"],
  "fidelity": "auto", 
  "chunks": [
    {
      "id": "node_742",
      "parent_id": "section_3",
      "logical_path": "Document/Section3/Paragraph2",
      "type": "Paragraph",
      "text": "The massive revenue spike is detailed in Table 4.",
      "bounding_box": { "page": 4, "x": 100, "y": 250, "w": 400, "h": 50 },
      "referenced_node_ids": ["table_4"],
      "vector": [0.12, -0.45, 0.88, ...]
    }
  ]
}
```

## 2. Structural Mappings (How JSON powers features)

1. **`document_group_id` & `version_number`:** Powers ADR 010 (Structural Diffing). The retrieval engine compares logical paths across versions of the same `document_group_id`.
2. **`allowed_document_groups`:** Powers Document-Level ACLs. The core forces a CrateDB array overlap `&&` with the user's `IRequestContext.AllowedDocumentGroups`.
3. **`fidelity`:** Powers ADR 006 (Graceful Degradation). Can be `auto` (default), `high`, or `standard`. If `auto`, the API evaluates if `parent_id` or `bounding_box` exists.
4. **`parent_id` & `logical_path`:** Powers Structural Expansion. Allows the retrieval engine to pull entire sections instead of sliding token windows.
5. **`referenced_node_ids`:** Powers ADR 009 (Multi-Hop RAG). Instructs the API to execute a secondary `SELECT` to fetch explicitly linked nodes (like `table_4`) without doing any text scanning.

## 3. The Canonical C# DTOs

The `TrueRag.Api` module, composed by `TrueRag.Host`, will map the JSON into these records, which are then dispatched to the WAL (via `IIngestionAcceptanceLog`) or to CrateDB directly (if using the sync path).

```csharp
namespace TrueRag.Core.Models;

public record IngestionRequestDto(
    string DocumentId,
    string DocumentGroupId,
    string VersionNumber,
    IReadOnlyCollection<string> AllowedDocumentGroups,
    string Fidelity = "auto", // "auto", "high", "standard"
    IReadOnlyCollection<ChunkDto> Chunks
);

public record ChunkDto(
    string Id,
    string? ParentId,
    string? LogicalPath,
    string Type,
    string Text,
    BoundingBoxDto? BoundingBox,
    IReadOnlyCollection<string>? ReferencedNodeIds,
    float[] Vector
);

public record BoundingBoxDto(
    int Page,
    float X,
    float Y,
    float W,
    float H
);
```

*(Note: TenantId and AppId are intentionally omitted from this DTO because they are supplied by the `IRequestContext` established by the Orchestrator's auth middleware, not trusted from the raw JSON body).*
