# TrueParserParquet - Compact Structured Output JSON Contract Specification

**Version**: `1.0`  
**Scope**: `TrueParserParquet` engine family  
**Input formats**: `parquet`  
**Use cases**: Analytics, Search, Lineage, Governance, RAG, Generative AI Pipelines

---

## 1. Purpose

This specification defines the **final client-facing JSON output contract** returned to consumers by **TrueParserParquet**.

The Parquet engine internally emits streamed NDJSON according to the **TrueParserParquet NDJSON Contract Specification**. The orchestration layer is responsible for transforming that internal NDJSON transport into the final compact JSON defined here. NDJSON is not part of the public consumer-facing contract.

This compact contract is optimized for downstream:

- search
- analytics
- lineage
- governance
- RAG
- GenAI pipelines

It preserves schema structure, row-group boundaries, row payload records, column-chunk detail, statistics, diagnostics, and provenance while avoiding an excessively wide per-record schema.

---

## 2. Design Principles

- Final JSON is the client contract.
- Compact but high-fidelity.
- Stable universal block shape.
- Parquet-specific details live under `attributes`.
- Schema tree order, row-group order, row order, and chunk order are preserved.
- Warnings are always surfaced.
- Output is deterministic.
- No duplicate canonical data across base fields and `attributes`.
- NDJSON transport details do not leak into the public contract.
- Document-level metadata and counts are hoisted from internal NDJSON header and summary data.
- Semantic tags remain distinguishable from source-declared metadata.
- Chunk hints are not part of the core contract.

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

| Field | Type | Rules |
| --- | --- | --- |
| `schema_version` | string | Always `1.0`. |
| `document` | object | Document-level metadata. Always present. |
| `warnings` | array<string> | Always present, even when empty. |
| `content` | array<object> | Ordered array of extracted content blocks. |

### Rules

- `document` MUST always be included.
- `warnings` MUST always be included, even when empty.
- `content` MUST preserve the original structural order represented by the internal NDJSON stream.

---

## 4. Document

```json
{
  "source_file": "orders.parquet",
  "document_name": "orders.parquet",
  "format": "parquet",
  "format_family": "parquet_file",
  "document_id": null,
  "content_hash": null,
  "is_partial": false,
  "metadata": {},
  "record_count": 12,
  "schema_node_count": 26,
  "leaf_column_count": 18,
  "row_group_count": 4,
  "row_record_count": 1250000,
  "column_chunk_count": 72,
  "statistics_count": 72,
  "key_value_metadata_count": 3,
  "semantic_tag_count": 5,
  "diagnostic_count": 1,
  "row_count": 1250000,
  "compressed_size": 38124567,
  "uncompressed_size": 74199210,
  "confidence": 0.99,
  "status": "success",
  "completeness": "complete"
}
```

### Fields

| Field | Type | Rules |
| --- | --- | --- |
| `source_file` | string \| null | File name only, not a path or URI. |
| `document_name` | string \| null | Human-friendly display name. Defaults to `source_file` when available. |
| `format` | string | Always `parquet`. |
| `format_family` | string | Always `parquet_file`. |
| `document_id` | string \| null | Optional stable identifier. |
| `content_hash` | string \| null | Optional orchestrator-computed content hash. |
| `is_partial` | boolean | Derived convenience flag indicating whether the document is not fully complete. Always present. |
| `metadata` | object | Always present. |
| `record_count` | integer \| null | Total number of emitted content blocks. |
| `schema_node_count` | integer \| null | Total schema node count. |
| `leaf_column_count` | integer \| null | Total leaf column count. |
| `row_group_count` | integer \| null | Total row-group count. |
| `row_record_count` | integer \| null | Total emitted `row` block count. |
| `column_chunk_count` | integer \| null | Total column-chunk count. |
| `statistics_count` | integer \| null | Total statistics block count. |
| `key_value_metadata_count` | integer \| null | Total file-level key/value metadata count. |
| `semantic_tag_count` | integer \| null | Total semantic tag count. |
| `diagnostic_count` | integer \| null | Total diagnostic count. |
| `row_count` | integer \| null | Total row count when available. |
| `compressed_size` | integer \| null | Total compressed size when available. |
| `uncompressed_size` | integer \| null | Total uncompressed size when available. |
| `confidence` | number \| null | Document-level confidence when available. |
| `status` | string \| null | Document-level extraction status such as `success`, `partial`, or `failed`. |
| `completeness` | string \| null | Document-level completeness such as `complete`, `partial`, or `incomplete`. |

### Rules

- `format` MUST always be `parquet`.
- `format_family` MUST always be `parquet_file`.
- `document_name` SHOULD default to `source_file` when available.
- `is_partial` MUST always be present and MUST be derived from the document's extraction state rather than treated as an independent source signal.
- `is_partial` SHOULD be `true` whenever `completeness` is `partial` or `incomplete`, or `status` is `partial` or `failed`.
- `is_partial` MAY be `false` only when the document is fully complete.
- `metadata` MUST always be present.
- `status`, `completeness`, and `confidence` are hoisted from the internal NDJSON header and summary when available.
- `schema_node_count`, `leaf_column_count`, `row_group_count`, `row_record_count`, `column_chunk_count`, `statistics_count`, `key_value_metadata_count`, `semantic_tag_count`, `diagnostic_count`, `row_count`, `compressed_size`, and `uncompressed_size` are hoisted from the internal NDJSON summary when available.
- Mode-specific rule: in `MetadataPlusRows`, `row_record_count` SHOULD be present and match the number of emitted `row` blocks.
- `is_partial` is a convenience signal and must not be used as the sole source of truth when `status` and `completeness` are available.
- `title`, `author`, `subject`, and `company` are not part of the compact Parquet envelope by default.

---

## 5. Warnings

```json
[
  "recovered after a malformed footer.",
  "One column chunk exposed incomplete statistics."
]
```

### Fields

| Field | Type | Rules |
| --- | --- | --- |
| `warnings` | string[] | Always present. |

### Rules

- Warnings MUST always be included, even when empty.
- Warnings are plain strings in the public contract.
- Top-level `warnings` are document-scoped summary notes only.
- Block `warnings` are block-local human-readable notes only.
- Use top-level warnings only when the issue is about the document as a whole or the extraction run as a whole.
- Use block warnings only when the note is specific to one block and does not need structured querying, correlation, or lifecycle tracking.
- Diagnostic blocks are the machine-consumable issue records and MUST be used when the condition has severity, code, kind, or needs downstream filtering, aggregation, replay, or correlation.
- Do not use warnings as a second diagnostics channel.
- The orchestrator MUST preserve diagnostic and warning text without inventing new structured warning codes in the public envelope.
- Document-scoped warnings should be derived from NDJSON diagnostics and summary/header state when they need to be surfaced at top level.
- If a condition is specific to one block and does not need severity or code, it may appear as a block warning.
- If a condition affects more than one block, or consumers need to query it structurally, emit a diagnostic block instead of a warning string.
- If the same issue is surfaced in both places, the warning text MUST stay brief and human-readable and MUST NOT duplicate the diagnostic payload.

---

## 6. Universal Base Block Contract

Every meaningful extracted unit in `content` MUST use this base shape.

```json
{
  "id": "col_0001",
  "type": "column",
  "order": 2,
  "path": ["column", "col_0005"],
  "parent_id": "schema_0008",
  "depth": 1,
  "source_ref": {
    "record_id": "col-order-items-price",
    "record_type": "column",
    "source_file": "orders.parquet",
    "schema_path": "order.items.price",
    "row_group_index": null,
    "column_ordinal": 5,
    "column_chunk_id": null,
    "data_page_offset": null,
    "dictionary_page_offset": null,
    "index_page_offset": null
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "order.items.price",
  "attributes": {}
}
```

### Fields

| Field | Type | Rules |
| --- | --- | --- |
| `id` | string | Deterministic block identifier. |
| `type` | string | Preserves the parser-emitted semantic type family. |
| `order` | integer | Preserves emitted traversal order and source order. |
| `path` | string[] \| null | Canonical public structural path, expressed as ordered segments. |
| `parent_id` | string \| null | Parent block identifier when applicable. |
| `depth` | integer | Structural depth. |
| `source_ref` | object \| null | Canonical normalized provenance object. |
| `is_inferred` | boolean | Indicates whether the block is best-effort or derived. |
| `warnings` | string[] | Always present on content blocks. |
| `content_hash` | string \| null | Optional orchestrator-computed content hash. |
| `text` | string \| null | Deterministic plain-text projection for downstream search and RAG. |
| `attributes` | object | Format-specific structured fields. |

### Rules

- `id` MUST be deterministic.
- `type` MUST preserve the parser-emitted semantic type family.
- `order` MUST preserve the parser-emitted traversal order and MUST match the block's position in source order.
- `path` is the canonical public structural path.
- `path` MUST always be an ordered segment array.
- The first segment of `path` MUST be the block type family name.
- Remaining segments MUST be stable identifiers or hierarchy labels for that family.
- `path` MUST NOT mix concatenated strings and split segments within the same family.
- `parent_id` is null only for root blocks.
- `depth` MUST never be null.
- `is_inferred` MUST never be null.
- `warnings` MUST always be present on content blocks, even when empty.
- `warnings` on a content block are block-local notes only and MUST NOT be used as a substitute for a diagnostic block.
- `content_hash` is optional and, when present, is computed by the orchestrator rather than the parser.
- `attributes` contains only type-specific fields not promoted into the universal base.
- `source_ref` is the canonical normalized provenance object.
- The orchestrator MUST NOT invent trace anchors the parser did not emit.

---

## 7. SourceRef

```json
{
  "record_id": "chunk-rg1-col-price",
  "record_type": "column_chunk",
  "source_file": "orders.parquet",
  "schema_path": "order.items.price",
  "row_group_index": 1,
  "column_ordinal": 5,
  "column_chunk_id": "chunk-rg1-col-price",
  "data_page_offset": 77812,
  "dictionary_page_offset": 76500,
  "index_page_offset": null
}
```

### Fields

| Field | Type | Rules |
| --- | --- | --- |
| `record_id` | string \| null | Parser-emitted identifier or equivalent stable source identifier. |
| `record_type` | string \| null | Source record family such as `schema_node`, `column`, or `column_chunk`. |
| `source_file` | string \| null | Source file name only. |
| `schema_path` | string \| null | Canonical schema path when available. |
| `row_group_index` | integer \| null | Row-group index when applicable. |
| `column_ordinal` | integer \| null | Leaf column ordinal when applicable. |
| `column_chunk_id` | string \| null | Column-chunk identifier when applicable. |
| `data_page_offset` | integer \| null | Data page offset when exposed. |
| `dictionary_page_offset` | integer \| null | Dictionary page offset when exposed. |
| `index_page_offset` | integer \| null | Index page offset when exposed. |

### Rules

- `source_ref` must preserve provenance without inventing new source anchors.
- The orchestrator may either emit only populated fields or normalize with explicit nulls, but it must stay consistent.
- Fields already normalized in `source_ref` MUST NOT be duplicated inside `attributes`.

---

## 8. Block Type Naming

Block types MUST preserve the original semantic families emitted by the Parquet parser.

### Current parser-emitted type families

- `schema_node`
- `column`
- `row_group`
- `row`
- `column_chunk`
- `statistics`
- `key_value_metadata`
- `semantic_tag`
- `diagnostic`

### Rules

- Do not rename block types unless required by a breaking public-contract revision.
- Do not collapse semantically distinct types.
- Keep structure, statistics, metadata, semantic tags, and diagnostics distinct.
- `summary` is not emitted as a content block; its information is hoisted into `document`.

---

## 9. Deterministic Text Mapping Rule

The public contract includes a universal text field for downstream search, RAG, and GenAI consumption.

The orchestrator MUST populate `text` using this deterministic priority order:

- for `schema_node` -> the public `path` or its canonical text projection
- for `column` -> the public `path` or its canonical text projection
- for `row_group` -> formatted fallback `row group {order or index}`
- for `row` -> deterministic payload projection from `attributes.values` when available
- for `column_chunk` -> formatted fallback `{path} chunk`
- for `statistics` -> formatted fallback `{path} min={MinValue} max={MaxValue}`
- for `key_value_metadata` -> formatted fallback `{Key}: {Value}`
- for `semantic_tag` -> `Tag`
- for `diagnostic` -> `Message`
- `null`

### Rules

- `text` is the canonical downstream plain-text projection.
- Empty or whitespace-only results at any priority step are treated as unmatched; the orchestrator MUST proceed to the next step.
- When `text` is derived from a non-primary field, the original structured field may remain in `attributes` only if it adds semantic value beyond the plain-text projection.
- The parser MUST NOT emit synthetic summaries or paraphrases in `text`.
- In this section, field names like `MinValue`, `Message`, and `Key` refer to the transformed public block values available to the orchestrator, not the internal NDJSON field names.

---

## 10. Attributes

`attributes` is a format-specific object containing additional structured fields for the block.

### Rules

- `attributes` should always be present.
- Use `{}` when no additional type-specific fields exist.
- `attributes` MUST contain only fields relevant to the current block type.
- `attributes` MUST preserve original structured values where meaningful.
- `attributes` MUST NOT repeat normalized provenance fields already present in `source_ref`.
- Public base fields should stay compact; Parquet-specific richness should live under `attributes`.

### Canonical examples

- Keep node kind, repetition, logical type, converted type, field IDs, and list/map flags on `schema_node`.
- Keep physical type, precision/scale, nullability, repetition, and index availability on `column`.
- Keep row counts, byte sizes, and ordinal bounds on `row_group`.
- Keep row ordinals, values map, and null-field paths on `row`.
- Keep compression, encodings, offsets, counts, and chunk-level link fields on `column_chunk`.
- Keep raw and normalized min/max values on `statistics`.
- Keep key/value pairs on `key_value_metadata`.
- Keep inferred semantic tags on `semantic_tag`.
- Keep severity, diagnostic kind, codes, and precise messaging on `diagnostic`.

---

## 11. Content Model

`content` is an ordered array of extracted content blocks.

Format-specific details must go inside `attributes`.

The array position preserves source order, and each block's `order` field provides the explicit source-order ordinal so consumers can detect filtered gaps or correlate back to the internal NDJSON stream.

### Supported type groups

Structural Types:

- `schema_node`
- `column`
- `row_group`
- `column_chunk`

Row Payload Types:

- `row`

Analytical Types:

- `statistics`

Semantic Types:

- `key_value_metadata`
- `semantic_tag`

Diagnostic Types:

- `diagnostic`

These groups and type names come directly from the internal Parquet NDJSON contract.

---

## 12. Fixed Compact Examples

### Schema node

```json
{
  "id": "schema_0008",
  "type": "schema_node",
  "order": 8,
  "path": ["schema_node", "schema-order-items-price"],
  "parent_id": "schema_0001",
  "depth": 3,
  "source_ref": {
    "record_id": "schema-order-items-price",
    "record_type": "schema_node",
    "source_file": "orders.parquet",
    "schema_path": "order.items.price"
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "order.items.price",
  "attributes": {
    "node_kind": "leaf",
    "repetition": "optional",
    "logical_type": "decimal",
    "converted_type": "decimal",
    "field_id": null,
    "max_definition_level": 1,
    "max_repetition_level": 0,
    "is_leaf": true
  }
}
```

### Column chunk

```json
{
  "id": "chunk_0001",
  "type": "column_chunk",
  "order": 14,
  "path": ["column_chunk", "chunk_0001"],
  "parent_id": "rg_0001",
  "depth": 2,
  "source_ref": {
    "record_id": "chunk-rg1-col-price",
    "record_type": "column_chunk",
    "source_file": "orders.parquet",
    "schema_path": "order.items.price",
    "row_group_index": 1,
    "column_ordinal": 5,
    "column_chunk_id": "chunk-rg1-col-price",
    "data_page_offset": 77812,
    "dictionary_page_offset": 76500,
    "index_page_offset": null
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "order.items.price chunk",
  "attributes": {
    "row_group_id": "rg-0001",
    "column_id": "col_0005",
    "compression": "snappy",
    "encodings": ["plain", "rle"],
    "value_count": 250000,
    "null_count": 1240,
    "distinct_count": null,
    "compressed_size": 231245,
    "uncompressed_size": 492110
  }
}
```

### Row

```json
{
  "id": "row_0001",
  "type": "row",
  "order": 13,
  "path": ["row", "row-rg1-000001"],
  "parent_id": "rg_0001",
  "depth": 2,
  "source_ref": {
    "record_id": "row-rg1-000001",
    "record_type": "row",
    "source_file": "orders.parquet",
    "row_group_index": 1
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "{\"order_id\":1001,\"price\":\"19.99\"}",
  "attributes": {
    "row_group_id": "rg-0001",
    "ordinal": 0,
    "row_group_ordinal": 0,
    "values": {
      "order_id": 1001,
      "price": "19.99"
    },
    "null_fields": []
  }
}
```

### Diagnostic

```json
{
  "id": "diag_0001",
  "type": "diagnostic",
  "order": 99,
  "path": ["diagnostic", "diag_0001"],
  "parent_id": null,
  "depth": 0,
  "source_ref": {
    "record_id": "diag-1",
    "record_type": "diagnostic",
    "source_file": "orders.parquet"
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "distinct_count was not exposed by the runtime surface for one or more column chunks.",
  "attributes": {
    "severity": "warning",
    "diagnostic_kind": "partial_statistics_surface",
    "code": "stats-001",
    "message": "distinct_count was not exposed by the runtime surface for one or more column chunks.",
    "schema_node_id": null,
    "column_id": "col_0005",
    "row_group_id": null,
    "column_chunk_id": "chunk_0001"
  }
}
```

---

## 13. Lossless Preservation Rules

The Parquet compact JSON output must preserve these invariants:

- one JSON object per content block
- document header first in the internal NDJSON source, with document hoisting in the final JSON
- `RecordType` only on the internal document header
- `Type` on all subsequent internal records
- deterministic output order
- schema tree order preserved
- row-group order preserved
- row order preserved within each row group
- leaf column identity preserved by `source_ref.schema_path` and stable column identifiers
- physical type and logical type remain distinguishable
- row-group and column-chunk boundaries remain distinguishable
- row payload values remain distinguishable from metadata and diagnostics
- statistics remain distinguishable from chunks and columns
- file-level key-value metadata remains first-class
- duplicate metadata entries are preserved when the source surface allows them
- inferred semantics remain distinguishable from source-declared metadata
- no silent loss of source-exposed metadata
- unsupported or unavailable surfaces signaled explicitly through completeness, diagnostics, or explicit nulls
- raw, normalized, and convenience representations remain separable when the source exposes more than one
- summary values are hoisted into `document`, not emitted as a content block

### Specific lossless requirements

- Nested schema structure must remain reconstructable from `schema_node` records.
- Leaf paths must remain stable and authoritative.
- Physical storage type must not be replaced by CLR/native projection type.
- Logical type annotations must not be flattened into display strings only.
- Chunk-level counts and sizes must remain separate from file-level totals.
- Statistics convenience values must not replace raw values where raw preservation is needed for fidelity.
- Source key-value metadata must not be merged into generic document metadata if that loses fidelity.
- List/map normalization must not destroy recoverability of the original shape.
- Any normalization step must preserve enough information to recreate the original Parquet-facing structure emitted by the runtime surface.

---

## 14. Null And Omission Rules

- Null fields may be omitted only when the omission is semantically equivalent to an explicit null for that field.
- If null, absent, and unknown mean different things for a record type, the field must be emitted explicitly and the distinction preserved.
- Required fields are always present.
- Optional fields may be omitted only when the omission does not remove source-visible meaning.

### Required fields by object type

| Block type | Required fields |
| --- | --- |
| `document` envelope | `schema_version`, `document`, `warnings`, `content` |
| `schema_node` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |
| `column` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |
| `row_group` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |
| `row` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |
| `column_chunk` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |
| `statistics` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |
| `key_value_metadata` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |
| `semantic_tag` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |
| `diagnostic` | `id`, `type`, `order`, `path`, `depth`, `is_inferred`, `warnings`, `attributes` |

---

## 15. Versioning

| Contract | Version | Field |
| --- | --- | --- |
| Parquet Compact Structured Output JSON | 1.0 | `schema_version` |

### Version notes

1. `1.0` aligns Parquet with the compact public contract style already used by other TrueParser families.
2. This does not imply field-for-field parity with SQL, PDF, CAD, or other families.
3. It aligns the top-level compact contract philosophy:
   - internal NDJSON is transport-only
   - downstream public JSON is compact and stable
   - structure stays explicit
   - output remains deterministic
