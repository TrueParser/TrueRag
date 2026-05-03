# TrueParserSql - Compact Structured Output JSON Contract Specification

**Version**: `1.0`  
**Scope**: TrueParserSql engine family  
**Input formats**: `sql`, `ddl`, `dml`, `script`  
**Use cases**: Analytics, Search, Lineage, Governance, RAG, Generative AI Pipelines  
**Package**: `package TrueParserSql --version 0.2.0`

---

## 1. Purpose

This specification defines the **final client-facing JSON output contract** returned to consumers by **TrueParserSql**.

The SQL engine internally emits streamed NDJSON according to the **TrueParserSql NDJSON Contract Specification**. The orchestration layer is responsible for transforming that internal NDJSON transport into the final compact JSON defined here. NDJSON is not part of the public consumer-facing contract.

This compact contract is optimized for downstream:

- search
- analytics
- lineage
- governance
- RAG
- GenAI pipelines

It preserves statement structure, semantics, lineage, diagnostics, and provenance while avoiding an excessively wide per-record schema.

---

## 2. Design Principles

- **Final JSON is the client contract**
- **Compact but high-fidelity**
- **Stable universal block shape**
- **SQL-specific details live under `attributes`**
- **statement boundaries and ordering are preserved**
- **Warnings are always surfaced**
- **Output is deterministic**
- **No duplicate canonical data across base fields and attributes**
- **Syntax, semantics, lineage, and diagnostics remain explicit**
- **NDJSON transport details do not leak into the public contract**
- **document-level stitched lineage remains distinguishable from statement-level lineage**
- **Parser-native statement and semantic classifications remain intact**

These principles follow the MailKit compact public style for envelope shape and the finalized SQL NDJSON contract for SQL semantics.

---

## 3. Top-Level Envelope

```json
{
  "schema_version": "1.0",
  "document": {},
  "warnings": [],
  "content": []
}
```
### Fields
schema_version - string, always "1.0"
document - document-level metadata
warnings - array of warning strings
content - ordered array of extracted content blocks
### Rules
document MUST always be included.
warnings MUST always be included, even when empty.
content MUST preserve the original structural order represented by the internal NDJSON stream.
## 4. Document
{
  "source_file": "script.sql",
  "document_name": "script.sql",
  "format": "sql",
  "format_family": "sql_script",
  "document_id": null,
  "content_hash": null,
  "mime_type": "text/sql",
  "title": null,
  "author": null,
  "subject": null,
  "company": null,
  "created_at": null,
  "modified_at": null,
  "is_partial": false,
  "metadata": {},
  "record_count": 5,
  "statement_count": 1,
  "successful_statement_count": 1,
  "failed_statement_count": 0,
  "read_statement_count": 1,
  "write_statement_count": 0,
  "define_statement_count": 0,
  "admin_statement_count": 0,
  "unknown_statement_count": 0,
  "relationship_count": 1,
  "lineage_edge_count": 1,
  "diagnostic_count": 0,
  "object_count": 2,
  "confidence": 1.0,
  "complexity_score": 4,
  "sensitivity_hit_count": 0,
  "status": "success",
  "completeness": "complete",
  "dialect": "PostgreSQL",
  "dialect_signals": [
    {
      "dialect": "PostgreSQL",
      "features": ["returning"],
      "notes": ["Detected PostgreSQL RETURNING clause support"],
      "best_effort": false
    }
  ],
  "object_registry": {
    "users": {
      "name": "users",
      "defined_in_statement": 1,
      "columns": ["id", "name"],
      "is_wildcard": false
    }
  },
  "options": {
    "lineage_mode": "statement",
    "parse_mode": "default"
  }
}
### Fields
source_file: string | null
document_name: string | null
format: string
format_family: "sql_script"
document_id: string | null
content_hash: string | null
mime_type: string | null
title: string | null
author: string | null
subject: string | null
company: string | null
created_at: string | null
modified_at: string | null
is_partial: boolean
metadata: object
record_count: integer | null
statement_count: integer | null
successful_statement_count: integer | null
failed_statement_count: integer | null
read_statement_count: integer | null
write_statement_count: integer | null
define_statement_count: integer | null
admin_statement_count: integer | null
unknown_statement_count: integer | null
relationship_count: integer | null
lineage_edge_count: integer | null
diagnostic_count: integer | null
object_count: integer | null
confidence: number | null
complexity_score: integer | null
sensitivity_hit_count: integer | null
status: string | null
completeness: string | null
dialect: string | null
dialect_signals: array<object> | null
object_registry: object | null
options: object
### Rules
format MUST always be "sql".
format_family MUST always be "sql_script".
document_name SHOULD default to source_file when available.
is_partial MUST always be present.
status and completeness are hoisted from the NDJSON document header when available.
confidence, complexity_score, sensitivity_hit_count, and statement-class counts are hoisted from the NDJSON terminal summary when available.
relationship_count is orchestrator-derived from emitted relationship content blocks and is not currently produced by the semantic summary.
diagnostic_count is orchestrator-derived from emitted diagnostic content blocks and is not currently produced by the semantic summary.
dialect_signals and object_registry are document-level public fields because the finalized NDJSON header already treats them as document-level first-class outputs.
object_registry is only available when the orchestrator runs document-level lineage analysis; statement-only mode may leave it null.
metadata MUST always be present.
options SHOULD reflect effective parse options that shape public output, especially parse mode and lineage mode when available.
options is orchestrator-injected from request context and is not serialized by the engine itself.
title, author, subject, and company are cross-engine envelope fields retained for consistency; for SQL they are normally null and MUST NOT be invented from code content.
dialect MUST preserve the engine-emitted casing and string value; the orchestrator MUST NOT normalize dialect strings to lowercase or any other alternate form.
## 5. Warnings
[
  "recovered after malformed token near FROM clause.",
  "Wildcard lineage was best-effort for one statement."
]
### Fields
warnings: string[]
### Rules
Warnings MUST always be included, even when empty.
Warnings are plain strings in the public contract.
The orchestrator MUST preserve diagnostic/warning text without inventing new structured warning codes in the public envelope.
document-scoped warnings should be derived from NDJSON diagnostics and summary/header state when they need to be surfaced at top level.
## 6. Universal Base Block Contract

Every meaningful extracted unit in content MUST use this base shape.

{
  "id": "stmt_0001",
  "type": "statement",
  "order": 1,
  "path": ["statement:1"],
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": {
    "statement_id": "stmt-1",
    "statement_index": 1,
    "source_file": "script.sql",
    "dialect": "PostgreSQL",
    "start_line": 1,
    "start_column": 1,
    "end_line": 5,
    "end_column": 20,
    "start_offset": 0,
    "end_offset": 128
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "SELECT u.name, SUM(o.total) AS total_spent FROM users u JOIN orders o ON u.id = o.user_id WHERE o.status = 'completed' GROUP BY u.name;",
  "attributes": {}
}
### Fields
id: string
type: string
order: integer
path: string[] | null
parent_id: string | null
depth: integer
page_number: integer | null
source_ref: SourceRef | null
is_inferred: boolean
warnings: string[]
content_hash: string | null
text: string | null
attributes: object
### Rules
id MUST be deterministic.
type MUST preserve the parser-emitted semantic type family.
order MUST preserve the parser-emitted traversal order and MUST match the block's position in source order.
path is the canonical public structural path, usually statement lineage such as ["statement:1"].
parent_id is null only for root blocks.
depth MUST never be null.
is_inferred MUST never be null.
warnings MUST always be present on content blocks, even when empty.
page_number remains a first-class base field for cross-engine consistency, but will always be null for SQL.
content_hash is optional and, when present, is computed by the orchestrator rather than the parser.
is_inferred MUST mirror the engine's best-effort/inference state: statements are false unless recovery or fallback analysis was required, entities and lineage edges are true when their underlying reference or edge is inferred, and relationship blocks should use false unless the orchestrator synthesizes them from analysis results.
For entity blocks, is_inferred is the public base-field mirror of the same source boolean carried as attributes.is_best_effort; the two values MUST match when present.
attributes contains only type-specific fields not promoted into the universal base.

This base shape follows the MailKit compact public style, while the content semantics come from the finalized SQL NDJSON contract.

## 7. SourceRef
{
  "statement_id": "stmt-1",
  "statement_index": 1,
  "source_file": "script.sql",
  "dialect": "PostgreSQL",
  "start_line": 1,
  "start_column": 1,
  "end_line": 5,
  "end_column": 20,
  "start_offset": 0,
  "end_offset": 128
}
### Fields
statement_id: string | null
statement_index: integer | null
source_file: string | null
dialect: string | null
start_line: integer | null
start_column: integer | null
end_line: integer | null
end_column: integer | null
start_offset: integer | null
end_offset: integer | null
### Rules
source_ref is the canonical normalized provenance object.
The orchestrator MUST NOT invent trace anchors the parser did not emit.
The engine currently emits only start-position location data; end positions and byte offsets are optional orchestrator-derived enrichments when available.
source_ref.dialect MUST use the same engine-emitted casing as document.dialect.
Fields already normalized in source_ref MUST NOT be duplicated inside attributes.
The orchestrator may either:
emit only populated fields, or
normalize all fields with explicit nulls

Pick one and keep it consistent.

#### Provenance Mapping from NDJSON

This public shape is derived primarily from NDJSON statement location fields and document metadata, especially Id, index, SourceFile, Dialect, StartLine, StartColumn, EndLine, EndColumn, StartOffset, and EndOffset.

## 8. Block Type Naming

Block types MUST preserve the original semantic names emitted by the SQL parser family.

#### Current parser-emitted type families

Examples include:

statement
entity
relationship
lineage_edge
diagnostic
### Rules
Do not rename block types unless required by a breaking public-contract revision.
Do not collapse semantically distinct types.
Keep statements, semantic entities, lineage edges, and diagnostics distinct.
## 9. Deterministic Text Mapping Rule

The public contract includes a universal text field for downstream search, RAG, and GenAI consumption.

The orchestrator MUST populate text using this deterministic priority order:

for statement -> Text
for relationship -> Expression
for diagnostic -> Message
for entity -> QualifiedName
for entity -> Name
for lineage_edge -> formatted fallback "${SourceColumn} -> ${TargetColumn}"
for lineage_edge -> formatted fallback "${Source} -> ${Target}"
null
### Rules
text is the canonical downstream plain-text projection.
Empty or whitespace-only results at any priority step are treated as unmatched; the orchestrator MUST proceed to the next step.
When text is derived from a non-primary field, the original structured field may remain in attributes only if it adds semantic value beyond the plain-text projection.
The parser MUST NOT emit synthetic summaries or paraphrases in text; the public contract inherits that rule.
## 10. Attributes

attributes is a format-specific object containing additional structured fields for the block.

### Rules
attributes should always be present.
Use {} when no additional type-specific fields exist.
attributes MUST contain only fields relevant to the current block type.
attributes MUST preserve original structured values where meaningful.
attributes MUST NOT repeat normalized provenance fields already present in source_ref.
Public base fields should stay compact; SQL-specific semantic richness should live under attributes.
document-level dialect signals and object registry stay in document, not in block attributes.
Canonical examples
Keep statement class, coverage, recovery, structure signals, confidence, sensitivity, and normalized text on statement.
Keep parser-computed statement_hash on statement when the engine emits it; statement_hash is distinct from orchestrator-computed content_hash and MUST NOT be conflated with it.
Keep entity kind, role, alias, schema/catalog breakdown, best-effort flags, resolved display values, and metadata on entity.
For entity blocks, attributes.is_best_effort MUST mirror the same source boolean used for the base-field is_inferred value.
Keep relationship kind, join type, source/target linkage, expression, and metadata on relationship.
Keep edge kind, scope, source/target statement indexes, source/target names, column lineage, and best-effort flags on lineage_edge.
For lineage_edge, source_entity_id and target_entity_id MUST reference actual emitted content-block ids when those endpoints exist; if a column-level endpoint is not emitted as a block, use null instead of a synthetic dangling id and rely on source_column and target_column.
resolved values are convenience-only display fields and may use display-formatted casing or normalization that differs from their canonical counterparts.
Keep severity, diagnostic kind, code, precise location, and metadata on diagnostic.
## 11. Content Model

content - ordered array of extracted content blocks

Format-specific details must go inside attributes.

The array position preserves source order, and each block's order field provides the explicit source-order ordinal so consumers can detect filtered gaps or correlate back to the internal NDJSON stream.

#### Supported type groups
##### Statement Types

Examples:

statement
##### Semantic Types

Examples:

entity
relationship
##### Lineage Types

Examples:

lineage_edge
##### Diagnostic Types

Examples:

diagnostic

These groups and type names come directly from the finalized SQL NDJSON contract.

## 12. Fixed Compact Examples
### Statement
{
  "id": "stmt_0001",
  "type": "statement",
  "order": 1,
  "path": ["statement:1"],
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": {
    "statement_id": "stmt-1",
    "statement_index": 1,
    "source_file": "script.sql",
    "dialect": "PostgreSQL",
    "start_line": 1,
    "start_column": 1,
    "end_line": 5,
    "end_column": 20,
    "start_offset": 0,
    "end_offset": 128
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "SELECT u.name, SUM(o.total) AS total_spent FROM users u JOIN orders o ON u.id = o.user_id WHERE o.status = 'completed' GROUP BY u.name;",
  "attributes": {
    "statement_kind": "select",
    "statement_class": "read",
    "coverage_state": "supported",
    "recovery_state": "clean",
    "normalized_text": "SELECT u.name, SUM(o.total) AS total_spent FROM users u JOIN orders o ON u.id = o.user_id WHERE o.status = 'completed' GROUP BY u.name;",
    "confidence": 1.0,
    "dialect_signals": [
      {
        "dialect": "PostgreSQL",
        "features": ["returning"],
        "notes": ["Detected PostgreSQL RETURNING clause support"],
        "best_effort": false
      }
    ],
    "structure_signals": {
      "join_count": 1,
      "subquery_count": 0,
      "cte_count": 0,
      "set_operation_count": 0,
      "aggregate_count": 1,
      "window_function_count": 0,
      "projection_count": 2,
      "filter_count": 1,
      "group_by_count": 1,
      "order_by_count": 0,
      "has_limit": false,
      "has_distinct": false,
      "complexity_score": 4
    },
    "sensitivity": [],
    "resolved": {
      "kind": "SELECT",
      "dialect": "PostgreSQL"
    },
    "metadata": {
      "has_cte": false,
      "has_subquery": false,
      "has_aggregation": true,
      "has_join": true
    }
  }
}
### Entity
{
  "id": "ent_0001",
  "type": "entity",
  "order": 2,
  "path": ["statement:1"],
  "parent_id": "stmt_0001",
  "depth": 1,
  "page_number": null,
  "source_ref": {
    "statement_id": "stmt-1",
    "statement_index": 1,
    "source_file": "script.sql",
    "dialect": "PostgreSQL"
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "public.users",
  "attributes": {
    "entity_kind": "table",
    "role": "read",
    "name": "users",
    "schema": "public",
    "catalog": null,
    "alias": "u",
    "qualified_name": "public.users",
    "is_best_effort": false,
    "resolved": {
      "display_name": "public.users"
    },
    "metadata": {
      "is_cte": false
    }
  }
}
### Relationship
{
  "id": "rel_0001",
  "type": "relationship",
  "order": 4,
  "path": ["statement:1"],
  "parent_id": "stmt_0001",
  "depth": 1,
  "page_number": null,
  "source_ref": {
    "statement_id": "stmt-1",
    "statement_index": 1,
    "source_file": "script.sql",
    "dialect": "PostgreSQL"
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "u.id = o.user_id",
  "attributes": {
    "relationship_kind": "join",
    "source_entity_id": "ent_0001",
    "target_entity_id": "ent_0002",
    "join_type": "inner",
    "expression": "u.id = o.user_id",
    "resolved": {},
    "metadata": {}
  }
}
### Lineage edge
{
  "id": "lin_0001",
  "type": "lineage_edge",
  "order": 5,
  "path": ["statement:1"],
  "parent_id": "stmt_0001",
  "depth": 1,
  "page_number": null,
  "source_ref": {
    "statement_id": "stmt-1",
    "statement_index": 1,
    "source_file": "script.sql",
    "dialect": "PostgreSQL"
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "orders.total -> total_spent",
  "attributes": {
    "edge_kind": "column_lineage",
    "scope": "statement",
    "source_entity_id": "ent_0002",
    "target_entity_id": null,
    "source_column": "orders.total",
    "target_column": "total_spent",
    "source_statement_index": null,
    "target_statement_index": null,
    "source": null,
    "target": null,
    "column": null,
    "is_best_effort": false,
    "resolved": {},
    "metadata": {
      "expression": "SUM(o.total)"
    }
  }
}
### Diagnostic
{
  "id": "diag_0001",
  "type": "diagnostic",
  "order": 6,
  "path": ["statement:2"],
  "parent_id": "stmt_0002",
  "depth": 1,
  "page_number": null,
  "source_ref": {
    "statement_id": "stmt-2",
    "statement_index": 2,
    "source_file": "script.sql",
    "dialect": "PostgreSQL",
    "start_line": 8,
    "start_column": 14,
    "end_line": 8,
    "end_column": 19
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "recovered after malformed token near FROM clause.",
  "attributes": {
    "severity": "warning",
    "diagnostic_kind": "unsupported_syntax",
    "code": "unexpected-token",
    "message": "recovered after malformed token near FROM clause.",
    "metadata": {}
  }
}
## 13. Statement Hierarchy in Public Output
### Rules
statement blocks are root-level content blocks and MUST have depth = 0.
entity, relationship, lineage_edge, and statement-scoped diagnostic blocks SHOULD use their parent statement as parent_id and MUST have depth = 1.
document-scoped stitched lineage_edge records may either:
be emitted with parent_id = null and depth = 0, or
be attached to a synthetic document root only in a future breaking revision

For version 1.0, keep document-scoped lineage edges root-level with parent_id = null.

## 14. Document-Level Stitched Lineage in Public Output

document-level stitched lineage is supported by the finalized NDJSON contract through lineage_edge records with Scope = document and explicit source/target statement indexes and object names.

### Rules
Stitched lineage remains in content as type = "lineage_edge".
For stitched lineage:
attributes.scope MUST be "document"
attributes.source_statement_index and attributes.target_statement_index MUST be preserved
attributes.source and attributes.target MUST be preserved
source_ref.statement_id may be null
Stitched lineage must not be collapsed into document metadata only.
document.object_registry may exist alongside stitched lineage, but neither replaces the other.
## 15. Invariants

The final JSON output must preserve these invariants:

Linear order preserved - output ordering must follow NDJSON structural order
Deterministic output - same source input must produce identical output
Truthful extraction - no summarization, paraphrasing, rewriting, or semantic invention
Structural fidelity - statement hierarchy must remain explicit via path, parent_id, and depth
Stable identifiers - IDs must be deterministic and stable
Source traceability - each meaningful block must be traceable via source_ref when available
Inference transparency - best-effort lineage and fallback extraction must remain explicit
Warning visibility - warnings must be included in final output
No synthetic filler - output must not invent meaningless grouping blocks
SQL semantic fidelity - statement class, entity kinds, lineage edges, diagnostics, and recovery states must remain distinct
No NDJSON leakage - transport-only header mechanics do not appear in the public block model

These invariants come from the finalized SQL NDJSON contract, with the public-envelope style borrowed from the MailKit compact reference.

## 16. Null and Omission Rules
Required fields

Required fields must always be present:

schema_version
document
warnings
content
block base fields
Optional fields

Optional fields may be omitted when not applicable.

#### document metadata

document - document-level metadata

### SourceRef

Choose one strategy and keep it consistent:

emit only populated fields, or
normalize all fields to explicit nulls
Attributes
attributes should always be present
use {} when no additional type-specific fields exist

These omission/null rules follow the compact public-envelope pattern from the MailKit reference.

## 17. Minimal Full Example
{
  "schema_version": "1.0",
  "document": {
    "source_file": "script.sql",
    "document_name": "script.sql",
    "format": "sql",
    "format_family": "sql_script",
    "document_id": null,
    "content_hash": null,
    "mime_type": "text/sql",
    "title": null,
    "author": null,
    "subject": null,
    "company": null,
    "created_at": null,
    "modified_at": null,
    "is_partial": false,
    "metadata": {},
    "record_count": 5,
    "statement_count": 1,
    "successful_statement_count": 1,
    "failed_statement_count": 0,
    "read_statement_count": 1,
    "write_statement_count": 0,
    "define_statement_count": 0,
    "admin_statement_count": 0,
    "unknown_statement_count": 0,
    "relationship_count": 1,
    "lineage_edge_count": 1,
    "diagnostic_count": 0,
    "object_count": 2,
    "confidence": 1.0,
    "complexity_score": 4,
    "sensitivity_hit_count": 0,
    "status": "success",
    "completeness": "complete",
    "dialect": "PostgreSQL",
    "dialect_signals": [],
    "object_registry": null,
    "options": {
      "lineage_mode": "statement",
      "parse_mode": "default"
    }
  },
  "warnings": [],
  "content": [
    {
      "id": "stmt_0001",
      "type": "statement",
      "order": 1,
      "path": ["statement:1"],
      "parent_id": null,
      "depth": 0,
      "page_number": null,
      "source_ref": {
        "statement_id": "stmt-1",
        "statement_index": 1,
        "source_file": "script.sql",
        "dialect": "PostgreSQL",
        "start_line": 1,
        "start_column": 1,
        "end_line": 5,
        "end_column": 20,
        "start_offset": 0,
        "end_offset": 129
      },
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "SELECT u.name, SUM(o.total) AS total_spent FROM users u JOIN orders o ON u.id = o.user_id WHERE o.status = 'completed' GROUP BY u.name;",
      "attributes": {
        "statement_kind": "select",
        "statement_class": "read",
        "coverage_state": "supported",
        "recovery_state": "clean",
        "normalized_text": "SELECT u.name, SUM(o.total) AS total_spent FROM users u JOIN orders o ON u.id = o.user_id WHERE o.status = 'completed' GROUP BY u.name;",
        "confidence": 1.0,
        "dialect_signals": [],
        "structure_signals": {
          "join_count": 1,
          "subquery_count": 0,
          "cte_count": 0,
          "set_operation_count": 0,
          "aggregate_count": 1,
          "window_function_count": 0,
          "projection_count": 2,
          "filter_count": 1,
          "group_by_count": 1,
          "order_by_count": 0,
          "has_limit": false,
          "has_distinct": false,
          "complexity_score": 4
        },
        "sensitivity": [],
        "resolved": {
          "kind": "SELECT",
          "dialect": "PostgreSQL"
        },
        "metadata": {
          "has_cte": false,
          "has_subquery": false,
          "has_aggregation": true,
          "has_join": true
        }
      }
    },
    {
      "id": "ent_0001",
      "type": "entity",
      "order": 2,
      "path": ["statement:1"],
      "parent_id": "stmt_0001",
      "depth": 1,
      "page_number": null,
      "source_ref": {
        "statement_id": "stmt-1",
        "statement_index": 1,
        "source_file": "script.sql",
        "dialect": "PostgreSQL"
      },
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "public.users",
      "attributes": {
        "entity_kind": "table",
        "role": "read",
        "name": "users",
        "schema": "public",
        "catalog": null,
        "alias": "u",
        "qualified_name": "public.users",
        "is_best_effort": false,
        "resolved": {
          "display_name": "public.users"
        },
        "metadata": {
          "is_cte": false
        }
      }
    },
    {
      "id": "ent_0002",
      "type": "entity",
      "order": 3,
      "path": ["statement:1"],
      "parent_id": "stmt_0001",
      "depth": 1,
      "page_number": null,
      "source_ref": {
        "statement_id": "stmt-1",
        "statement_index": 1,
        "source_file": "script.sql",
        "dialect": "PostgreSQL"
      },
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "public.orders",
      "attributes": {
        "entity_kind": "table",
        "role": "read",
        "name": "orders",
        "schema": "public",
        "catalog": null,
        "alias": "o",
        "qualified_name": "public.orders",
        "is_best_effort": false,
        "resolved": {
          "display_name": "public.orders"
        },
        "metadata": {
          "is_cte": false
        }
      }
    },
    {
      "id": "rel_0001",
      "type": "relationship",
      "order": 4,
      "path": ["statement:1"],
      "parent_id": "stmt_0001",
      "depth": 1,
      "page_number": null,
      "source_ref": {
        "statement_id": "stmt-1",
        "statement_index": 1,
        "source_file": "script.sql",
        "dialect": "PostgreSQL"
      },
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "u.id = o.user_id",
      "attributes": {
        "relationship_kind": "join",
        "source_entity_id": "ent_0001",
        "target_entity_id": "ent_0002",
        "join_type": "inner",
        "expression": "u.id = o.user_id",
        "resolved": {},
        "metadata": {}
      }
    },
    {
      "id": "lin_0001",
      "type": "lineage_edge",
      "order": 5,
      "path": ["statement:1"],
      "parent_id": "stmt_0001",
      "depth": 1,
      "page_number": null,
      "source_ref": {
        "statement_id": "stmt-1",
        "statement_index": 1,
        "source_file": "script.sql",
        "dialect": "PostgreSQL"
      },
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "orders.total -> total_spent",
      "attributes": {
        "edge_kind": "column_lineage",
        "scope": "statement",
        "source_entity_id": "ent_0002",
        "target_entity_id": null,
        "source_column": "orders.total",
        "target_column": "total_spent",
        "source_statement_index": null,
        "target_statement_index": null,
        "source": null,
        "target": null,
        "column": null,
        "is_best_effort": false,
        "resolved": {},
        "metadata": {
          "expression": "SUM(o.total)"
        }
      }
    }
  ]
}
## 18. Implementation Notes for the Orchestrator

The orchestrator must:

preserve stable IDs, statement order, hierarchy, warnings, and inference markers
keep page_number present and always null for SQL
preserve or normalize source_ref
derive depth from statement-root hierarchy, with statement blocks using 0
hoist document header and summary information into document
compute record_count from emitted content blocks
preserve original block type names without renaming
map SQL-specific fields into attributes without dropping meaningful semantics
apply the deterministic text mapping rule consistently
normalize enum-style semantic values from NDJSON PascalCase to public snake_case_lower in the final JSON materialization layer, including values such as coverage_state, entity_kind, role, join_type, severity, and diagnostic_kind, while preserving the underlying meaning
ensure depth and is_inferred are never null
preserve document-level stitched lineage as content blocks rather than collapsing it into metadata
keep the public envelope compact while preserving parser-native classifications

These responsibilities come from the SQL NDJSON contract, with the public client shape patterned after the MailKit compact reference.

## 19. Versioning
Version	Change
1.0	Initial compact public structured output JSON contract for TrueParserSql

