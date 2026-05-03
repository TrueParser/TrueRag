# TrueParser.OpenDoc - Compact Structured Output JSON Contract Specification

**Version**: `1.0`  
**Scope**: TrueParser.OpenDoc engine family  
**Input formats**: `odt`, `ods`, `odp`, `odg`, `odf`, `fodt`, `fods`, `fodp`, `epub`, `idml`, `dbf`, `dif`, `mif`  
**Package to use**: package `TrueParser.OpenDoc --version 2.2.0` for implementation of this contract  
**Use cases**: Analytics · Search · RAG · Generative AI Pipelines

---

## 1. Purpose

This specification defines the **final client-facing JSON output contract** returned to consumers by **TrueParser.OpenDoc**.

The OpenDoc engine internally emits streamed NDJSON according to the **TrueParser.OpenDoc NDJSON Contract Specification**. The orchestration layer is responsible for transforming that internal NDJSON transport into the final compact JSON defined here. NDJSON is not part of the public consumer-facing contract. :contentReference[oaicite:2]{index=2}

This compact contract is optimized for downstream:

- search
- analytics
- RAG
- GenAI pipelines

It preserves structure, provenance, and fidelity while avoiding an excessively wide per-record schema. As in your MS Office compact contract, the public shape is a stable envelope with a universal base block contract, while format-specific details live under `attributes`. :contentReference[oaicite:3]{index=3}

---

## 2. Design Principles

- **Final JSON is the client contract**
- **Compact but high-fidelity**
- **Stable universal block shape**
- **Format-specific details live under `attributes`**
- **Hierarchy and provenance are preserved**
- **Chunk hints are optional and request-controlled**
- **Warnings are always surfaced**
- **Output is deterministic**
- **No duplicate canonical data across base fields and attributes**
- **OpenDoc-family types remain parser-native**
- **NDJSON transport details do not leak into the public contract**

These principles are consistent with your OpenDoc NDJSON contract: one document header internally, then typed structural records, no parser-side splitting, no truncation, no consumer-shaped chunking, and preservation of source-native structural units. :contentReference[oaicite=4]{index=4}

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

- `schema_version` - string, always `"1.0"`
- `document` - document-level metadata
- `warnings` - array of warning strings
- `content` - ordered array of extracted content blocks

### Rules

- `content` MUST preserve the original structural order represented by the internal NDJSON stream.
- `warnings` MUST always be included, even when empty.
- `document` MUST always be included.

---

## 4. Document

```json
{
  "source_file": "sample.ods",
  "document_name": "sample.ods",
  "format": "ods",
  "format_family": "spreadsheet",
  "title": null,
  "author": null,
  "subject": null,
  "company": null,
  "created_at": null,
  "modified_at": null,
  "is_partial": false,
  "metadata": {},
  "page_count": null,
  "sheet_count": 3,
  "slide_count": null,
  "record_count": null
}
```

### Fields

- `source_file`: `string | null`
- `document_name`: `string | null`
- `format`: `string`
- `format_family`: `"text_document" | "spreadsheet" | "presentation" | "drawing" | "formula" | "publication_package" | "data_table" | "layout_document" | "odf_generic"`
- `title`: `string | null`
- `author`: `string | null`
- `subject`: `string | null`
- `company`: `string | null`
- `created_at`: `string | null`
- `modified_at`: `string | null`
- `is_partial`: `boolean`
- `metadata`: `object`
- `page_count`: `integer | null`
- `sheet_count`: `integer | null`
- `slide_count`: `integer | null`
- `record_count`: `integer | null`

### `format_family` Mapping

The orchestrator derives `format_family` from `format` using this mapping:

- `flat_odf` -> `odf_generic` unless the parser or container metadata identifies a concrete ODF subtype
- `odt`, `fodt` -> `text_document`
- `ods`, `fods`, `dif` -> `spreadsheet`
- `odp`, `fodp` -> `presentation`
- `odg` -> `drawing`
- `odf` -> `formula` only when the parser or container metadata identifies a concrete formula document; otherwise `odf_generic`
- `epub`, `idml` -> `publication_package`
- `dbf` -> `data_table`
- `mif` -> `layout_document`

### Rules

- `format` MUST preserve the concrete source subtype from the parser contract, such as `odt`, `ods`, or `epub`.
- `format` MAY be a parser fallback such as `odf` or `flat_odf` when the parser cannot determine a more specific subtype from the source.
- `format_family` MUST NOT guess `formula` for generic `odf` or `flat_odf` fallbacks unless the parser or surrounding metadata actually identifies a formula document.
- `document_name` MUST preserve the parser-assigned document name when available and MUST remain distinct from metadata-derived `title`.
- `is_partial` MUST reflect the parser's disclosure signal for incomplete extraction.
- `is_partial` SHOULD be `false` for clean extractions and `true` when the parser reports a degraded or incomplete result.
- `is_partial` MUST always be present in the public document object.
- `document_name` MUST always be present in the public document object.
- `document_name` may be `null` if the parser does not provide one.
- `title`, `author` or `creator`, `subject`, and `company` may be hoisted from document/package metadata records where present.
- `metadata` MUST preserve any parser-emitted document metadata keys that do not map to named public fields.
- `metadata` values SHOULD remain strings unless a future parser version emits typed document metadata.
- `created_at` and `modified_at` may be `null` if unavailable.
- `page_count`, `sheet_count`, `slide_count`, and `record_count` are optional computed summary values.
- All document metadata fields SHOULD always be present in the public contract, even when `null`, matching the compact style used in the MS Office public contract.

---

## 5. Warnings

```json
[
  "Navigation structure was partially recoverable."
]
```

### Fields

- `warnings`: `string[]`

### Rules

- Warnings MUST always be included when present, even if empty.
- Warnings are plain strings in the current OpenDoc parser surface.
- The orchestrator MUST preserve warning text without inventing structured warning codes or sources unless a future parser version emits them.
- Warnings are disclosures only. They do not legitimize parser-side splitting, truncation, or fidelity loss, consistent with the NDJSON contract.

---

## 6. Universal Base Block Contract

Every meaningful extracted unit in `content` MUST use this base shape.

```json
{
  "id": "blk_000123",
  "type": "paragraph",
  "path": "OEBPS/chapter1.xhtml",
  "parent_id": "blk_000100",
  "depth": 2,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": "Coverage details",
  "attributes": {}
}
```

### Fields

- `id`: `string`
- `type`: `string`
- `path`: `string | null`
- `parent_id`: `string | null`
- `depth`: `integer`
- `page_number`: `integer | null`
- `source_ref`: `SourceRef | null`
- `is_inferred`: `boolean`
- `chunk_hint`: `boolean` - present only when `chunkHints=true`
- `text`: `string | null`
- `attributes`: `object`

### Rules

- `id` MUST be deterministic.
- `type` MUST preserve the parser-emitted normalized type.
- The orchestrator MUST NOT invent format-specific aliases that the parser does not emit.
- `path` is the canonical public source path string, not a semantic breadcrumb array.
- `path` MUST be copied from the parser-emitted `Path` value when available.
- `parent_id` is `null` only for root blocks.
- `depth` MUST never be `null` in the public contract.
- `is_inferred` MUST never be `null` in the public contract.
- `page_number` remains a first-class field.
- `is_inferred` is a public client-facing flag. In the current parser surface it MUST be `false` for all blocks because no inference signal is emitted yet.
- `attributes` contains only type-specific fields not promoted into the universal base.
- `chunk_hint` MUST be omitted entirely when `chunkHints=false` or when the option is not supplied.
- The public base shape follows the same design pattern as your MS Office compact contract, but with OpenDoc-family provenance extensions.

---

## 7. SourceRef

```json
null
```

### Current Behavior

- `source_ref` is optional and may be `null`.
- The current OpenDoc parser surface does not emit structured source-anchor objects, so the orchestrator MUST NOT synthesize sheet, row, column, slide, spine, story, frame, record, or field anchors.
- When the parser emits a lossless path-like trace value, the orchestrator MAY carry it forward as `package_path`.
- `name` MAY be used only when it is directly backed by parser-emitted metadata.

### Reserved future anchors

The following fields are reserved for a future parser version that explicitly emits them:

- `page`
- `sheet`
- `row_index`
- `column_index`
- `slide_number`
- `address`
- `range_address`
- `spine_index`
- `story_id`
- `frame_id`
- `record_index`
- `field_name`

### Rules

- `source_ref` is optional provenance data, not a guaranteed fully populated anchor object.
- The orchestrator MUST NOT invent trace anchors that the parser did not emit.
- When `source_ref` is `null`, it MUST remain `null` rather than being replaced with fabricated empty fields.
- Any parser-backed provenance that is surfaced in `source_ref` MUST NOT be duplicated inside `attributes`.

### Provenance Mapping from NDJSON

This shape is derived only from parser-emitted trace data. In the current implementation that means `Path` and any other explicit metadata fields already present in the record, not spreadsheet-style or layout-style anchors fabricated by the orchestrator.

---

## 8. Block Type Naming

Block types MUST preserve the original semantic names emitted by the OpenDoc parser.

The current parser-emitted block vocabulary is intentionally small and stable:

- `section`
- `heading`
- `paragraph`
- `list`
- `list_item`
- `code`
- `table`
- `row`
- `cell`

Do not remap these values to format-specific aliases such as `worksheet`, `spreadsheet_cell`, `spine_item`, `story`, `frame`, or `field_value` unless a future breaking version explicitly adds parser support for those types.

### Rules

- Do not rename block types unless required by a breaking public-contract revision.
- Do not collapse semantically distinct types.
- Prefer the current parser-emitted names listed above, and do not introduce broader format-specific aliases unless a future breaking revision adds parser support for them.

---

## 9. Deterministic `text` Mapping Rule

The public contract includes a universal `text` field for downstream search, RAG, and GenAI consumption.

In the current OpenDoc parser surface, `Text` is the only guaranteed source field, so the orchestrator will usually project `text` directly from `Text`.

The fallback priority order below is future-facing and applies only if a future parser version emits richer source fields:

1. `Text`
2. `Value` (converted to string when needed)
3. formula expression text
4. `Title`
5. if block has `Entries`, `Texts`, `Items`, or equivalent string-list content, join with newlines
6. `AltText`
7. `Name`
8. if block is a list, concatenate item texts with newlines
9. `null`

### Rules

- `text` is the canonical downstream plain-text projection.
- In the current OpenDoc parser surface, `text` will usually mirror `Text` because no richer public text-bearing fields are emitted today.
- Empty or whitespace-only results at any priority step are treated as unmatched; the orchestrator MUST proceed to the next step.
- The fallback chain exists for forward compatibility and must not be read as evidence that the parser currently emits `Value`, `Title`, `AltText`, `Name`, `Entries`, `Texts`, or `Items` as distinct public fields.
- When `text` is derived from a non-`Text` property, the original structured field may remain in `attributes` only if it adds semantic value beyond the plain-text projection.
- Do not duplicate identical content in both `text` and `attributes` unless the structured field is semantically distinct.
- Inline run structure is not currently preserved as a separate public field in the OpenDoc compact contract; run-level content is flattened into `text` unless a future parser version emits a structured run collection.
- The parser MUST NOT emit Markdown or other synthetic formatting syntax inside text fields, and the public contract inherits that rule.

---

## 10. Attributes

`attributes` is a format-specific object containing additional parser-emitted metadata fields for the block.

In the current OpenDoc parser surface, attribute values are string-valued because the underlying `BlockNode.Attributes` collection is string-to-string.

This keeps the public contract compact while preserving fidelity.

### Rules

- `attributes` should always be present.
- Use `{}` when no additional type-specific fields exist.
- `attributes` MUST contain only fields relevant to the current block type.
- `attributes` MUST preserve original string values where meaningful.
- The orchestrator MUST NOT infer integer, boolean, or numeric attribute types unless a future parser version emits a typed metadata schema.
- Document-level fields such as `format` and `format_family` MUST NOT be duplicated inside block `attributes`.
- `attributes` MUST NOT repeat normalized provenance fields already present in `source_ref`.
- `attributes` MUST NOT repeat the canonical `text` value unless the original field is semantically distinct.

### Canonical examples

- Keep `level` and `style` in `attributes` for a heading.
- Keep formula result metadata in string form for formula-like records.
- Keep `items` for a list.
- Keep shape, connector, alignment, table, image, chart, or frame metadata where meaningful.
- Keep inline run metadata only if a future parser version emits a structured run collection; the current compact contract does not expose `runs`.
- Keep story/frame linkage metadata for IDML.
- Keep DBF schema and field metadata using parser-emitted names such as `name` and `dbfType`.
- Keep spreadsheet cell metadata only when the parser actually emits it; the current compact contract does not guarantee keys like `column` or `data_type`.

This mirrors the public compact design you used for MS Office, where the base block stays stable and the rest is stored under `attributes`.

---

## 11. Content Model

`content` is always an ordered array of blocks using the universal base contract.

Format-specific details must go inside `attributes`.

### Current parser-emitted `type` values

The public compact contract currently exposes only the parser-emitted normalized values listed above.

Format-specific distinctions still matter, but they belong in `attributes` and `source_ref`, not in an expanded public `type` registry.

Examples:

- `section`
- `heading`
- `paragraph`
- `list`
- `list_item`
- `code`
- `table`
- `row`
- `cell`

Any richer registry for `worksheet`, `spreadsheet_cell`, `spreadsheet_formula`, `slide`, `presentation_paragraph`, `story`, `frame`, `field_value`, `spine_item`, or `drawing_page` would require a future breaking contract revision backed by corresponding parser output.

---

## 12. Fixed Compact Examples

### Text-document heading

```json
{
  "id": "blk_0001",
  "type": "heading",
  "path": "content.xml",
  "parent_id": null,
  "depth": 0,
  "page_number": 1,
  "source_ref": null,
  "is_inferred": false,
  "text": "Coverage",
  "attributes": {
    "level": "1",
    "style": "Heading 1"
  }
}
```

### Spreadsheet cell

```json
{
  "id": "blk_cell_001",
  "type": "cell",
  "path": "content.xml",
  "parent_id": "blk_row_001",
  "depth": 2,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": "Name",
  "attributes": {}
}
```

### Spreadsheet formula-like cell

```json
{
  "id": "blk_formula_001",
  "type": "cell",
  "path": "content.xml",
  "parent_id": "blk_cell_002",
  "depth": 3,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": "6",
  "attributes": {}
}
```

### Presentation section

```json
{
  "id": "blk_slide_001",
  "type": "section",
  "path": "content.xml",
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": "Quarterly Review",
  "attributes": {}
}
```

### EPUB paragraph

```json
{
  "id": "blk_epub_001",
  "type": "paragraph",
  "path": "OEBPS/chapter1.xhtml",
  "parent_id": "blk_spine_001",
  "depth": 1,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": "It was a dark and stormy night.",
  "attributes": {}
}
```

### IDML section

```json
{
  "id": "blk_story_001",
  "type": "paragraph",
  "path": "Stories/Story_12.xml",
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": null,
  "attributes": {
    "style": "ParagraphStyle/NormalParagraphStyle"
  }
}
```

### DBF cell

```json
{
  "id": "blk_field_001",
  "type": "cell",
  "path": "dbf/row/0",
  "parent_id": "blk_record_001",
  "depth": 1,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": "1001",
  "attributes": {
    "name": "CustomerId",
    "dbfType": "N"
  }
}
```

---

## 13. Chunk Hint Behavior

### Request behavior

- `chunkHints` defaults to `false`
- `chunkHints=true` -> eligible blocks may include `chunk_hint`, but only if a future parser version or upstream enrichment pass actually marks candidate blocks
- `chunkHints=false` -> `chunk_hint` must be omitted from all blocks

### Rule

The current OpenDoc parser surface does not compute chunk-boundary candidates. In the compact contract, `chunk_hint` is a request-controlled exposure of parser- or enrichment-provided data, not a guarantee that such data exists.

This follows the same public behavior you defined in the MS Office compact contract, while respecting that the parser contract itself is not consumer-chunking oriented.

---

## 14. Invariants

The final JSON output must preserve these invariants:

- **Linear order preserved** - output ordering must follow source reading or structural order
- **Deterministic output** - same source input must produce identical output
- **Truthful extraction** - no summarization, paraphrasing, rewriting, or semantic invention
- **Structural fidelity** - hierarchy must remain explicit via `path`, `parent_id`, and `depth`
- **Stable identifiers** - IDs must be deterministic and stable
- **Source traceability** - each meaningful block must be traceable via `source_ref` when available
- **Inference transparency** - if a future parser version emits inference signals, inferred structure must be marked with `is_inferred=true`; in the current OpenDoc parser surface, this field remains `false`
- **Warning visibility** - warnings must be included in final output
- **No synthetic filler** - output must not invent empty grouping blocks or meaningless wrappers
- **No parser-side split/truncate leakage** - public output must not normalize or disguise parser-side fragmentation because the parser contract forbids it

These invariants are consistent with both your OpenDoc NDJSON spec and your MS Office compact output design.

---

## 15. Null and Omission Rules

### Required fields

Required fields must always be present:

- `schema_version`
- `document`
- `warnings`
- `content`
- block base fields except `chunk_hint` when disabled or not requested

### Optional fields

Optional fields may be omitted when not applicable.

### Document metadata

Document metadata fields should always be present in `document`, even when `null`.
- `metadata` should always be present and should carry any unmapped parser-emitted document metadata key/value pairs.
- Do not duplicate a value inside `metadata` if it already has a named public field, unless the parser emits a distinct raw key that must be preserved for fidelity.
- Preserve parser-emitted metadata key casing as-is, including camelCase keys such as `contentType` and `manifestPresent`.

### SourceRef

- `source_ref` MAY be `null` when no parser-backed anchor exists.
- Do not fabricate populated fields to satisfy the shape.
- If the parser later emits a backed anchor, the orchestrator may carry it through, but only for fields that are explicitly source-backed.

### Attributes

- `attributes` should always be present
- use `{}` when no additional type-specific fields exist

These omission/null rules follow the compact public-contract style already established in your MS Office JSON spec, while keeping `source_ref` honest about the current OpenDoc parser surface.

---

## 16. Minimal Full Example

```json
{
  "schema_version": "1.0",
  "document": {
    "source_file": "sample.ods",
    "document_name": "sample.ods",
    "format": "ods",
    "format_family": "spreadsheet",
    "title": null,
    "author": null,
    "subject": null,
    "company": null,
    "created_at": null,
    "modified_at": null,
    "is_partial": false,
    "metadata": {
      "contentType": "application/vnd.oasis.opendocument.spreadsheet",
      "manifestPresent": "true"
    },
    "page_count": null,
    "sheet_count": 1,
    "slide_count": null,
    "record_count": 4
  },
  "warnings": [],
  "content": [
    {
      "id": "blk_0001",
      "type": "section",
      "path": "content.xml",
      "parent_id": null,
      "depth": 0,
      "page_number": null,
      "source_ref": null,
      "is_inferred": false,
      "text": "Sheet1",
      "attributes": {}
    },
    {
      "id": "blk_0002",
      "type": "row",
      "path": "content.xml",
      "parent_id": "blk_0001",
      "depth": 1,
      "page_number": null,
      "source_ref": null,
      "is_inferred": false,
      "text": null,
      "attributes": {}
    },
    {
      "id": "blk_0003",
      "type": "cell",
      "path": "content.xml",
      "parent_id": "blk_0002",
      "depth": 2,
      "page_number": null,
      "source_ref": null,
      "is_inferred": false,
      "text": "Revenue",
      "attributes": {}
    },
    {
      "id": "blk_0004",
      "type": "cell",
      "path": "content.xml",
      "parent_id": "blk_0002",
      "depth": 2,
      "page_number": null,
      "source_ref": null,
      "is_inferred": false,
      "text": "150000",
      "attributes": {}
    }
  ]
}
```

---

## 17. Implementation Notes for the Orchestrator

The orchestrator must:

- derive `format_family` from `format`
- preserve stable IDs, hierarchy, warnings, and inference markers
- preserve `page_number` as a first-class base field
- leave `source_ref` null unless the parser has emitted a lossless backing anchor that can be carried forward without invention
- keep `is_inferred` present and set it to `false` unless a future parser version emits explicit inference metadata
- hoist `title`, `author`, `subject`, and `company` from metadata records where present
- map parser-emitted `creator` metadata to document `author` when a dedicated author field is not already present
- compute `sheet_count`, `slide_count`, and `record_count` from the parser-emitted structure and document metadata where available
- default unsupported metadata such as `created_at` and `modified_at` to `null`
- expose `chunk_hint` only when `chunkHints=true`
- preserve original block type names without renaming
- map type-specific fields into `attributes` without dropping meaningful semantics
- apply the deterministic `text` mapping rule consistently
- ensure `depth` and `is_inferred` are never null in the public contract
- derive `source_ref` only when the parser already emitted a lossless backing anchor or explicit metadata field that can be passed through without fabrication

These responsibilities mirror the orchestrator role described in both source documents: internal NDJSON transport in the engine, public compact JSON for clients.

---

## 18. Versioning

| Version | Change |
| --- | --- |
| `1.0` | Initial compact public structured output JSON contract for TrueParser.OpenDoc |

The main decisions I made were:

- keep the **same public compact envelope pattern** as your MS Office spec, because that is already the client-facing style you settled on, :contentReference[oaicite:19]{index=19}
- keep the **parser-emitted type vocabulary compact and stable**, rather than advertising an unimplemented source-specific registry. :contentReference[oaicite:20]{index=20}

One thing you may want to decide next is whether `format_family` should stay broad and user-facing as written here, or whether you want tighter families like `odf_text`, `odf_spreadsheet`, `publication_package`, and `legacy_data_exchange`.
