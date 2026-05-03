# TrueParser — Compact Output JSON Contract Specification -Use MS office package version TrueParserMsOffice --version 0.3.2

**Version**: 1.0  
**Scope**: MS Office + Web/Text  
**Input formats**: `docx`, `doc`, `rtf`, `dotx`, `docm`, `xlsx`, `xls`, `csv`, `tsv`, `xlsm`, `pptx`, `ppt`, `pptm`, `potx`, `potm`, `html`, `htm`, `md`, `txt`

**Use cases**: Analytics · Search · RAG · Generative AI Pipelines

---

## 1. Purpose

This specification defines the final JSON output contract returned to consumers by TrueParser.

Parsing engines internally emit streamed NDJSON according to the [Internal Engine NDJSON Specification](file:///d:/Development/TrueParser/internalContract.md) for transport and orchestration. The orchestration layer is responsible for transforming that raw internal output into the final compact JSON defined here. NDJSON is not part of the public consumer-facing contract.

This compact contract is optimized for downstream:

- search
- analytics
- RAG
- GenAI pipelines

It preserves structure, provenance, and fidelity while avoiding an excessively wide per-block schema.

---

## 2. Design Principles

- **Final JSON is the consumer contract**
- **Compact but high-fidelity**
- **Stable universal block shape**
- **Format-specific details live under `attributes`**
- **Hierarchy and provenance are preserved**
- **Chunk hints are optional and request-controlled**
- **Warnings are always surfaced**
- **Output is deterministic**
- **No duplicate canonical data across base fields and attributes**

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

````

### Fields

- `schema_version` — string, always `"1.0"`
- `document` — document-level metadata
- `warnings` — array of warning objects
- `content` — ordered array of extracted content blocks

---

## 4. Document

```json
{
  "source_file": "sample.docx",
  "format": "docx",
  "format_family": "word",
  "title": "Insurance Policy",
  "author": "Jane Smith",
  "subject": "Coverage Terms",
  "company": "Acme Corp",
  "created_at": null,
  "modified_at": null,
  "page_count": null,
  "sheet_count": null,
  "slide_count": null
}
```

### Fields

- `source_file`: `string | null`
- `format`: `string`
- `format_family`: `"word" | "excel" | "powerpoint" | "web_text"`
- `title`: `string | null`
- `author`: `string | null`
- `subject`: `string | null`
- `company`: `string | null`
- `created_at`: `string | null`
- `modified_at`: `string | null`
- `page_count`: `integer | null`
- `sheet_count`: `integer | null`
- `slide_count`: `integer | null`

### Rules

- `format_family` is derived by the orchestrator from `format`.
- `title` and `author` may be taken from document header metadata.
- `subject` and `company` may be hoisted from metadata blocks where present.
- `created_at` and `modified_at` may be `null` if unavailable.
- `page_count`, `sheet_count`, and `slide_count` are optional computed summary values.

---

## 5. Warnings

```json
{
  "code": "LEGACY_XLS_FORMULA_LOSS",
  "message": "Legacy Excel workbook could not preserve all formula metadata.",
  "source": "ExcelEngine"
}
```

### Fields

- `code`: `string`
- `message`: `string`
- `source`: `string | null`

Warnings must always be included when present.

---

## 6. Universal Base Block Contract

Every meaningful extracted unit in `content` must use this base shape.

```json
{
  "id": "blk_000123",
  "type": "paragraph",
  "path": ["Section 1", "Coverage"],
  "parent_id": "blk_000100",
  "depth": 2,
  "page_number": 3,
  "source_ref": {
    "page": 3
  },
  "is_inferred": false,
  "chunk_hint": true,
  "text": "Coverage details",
  "attributes": {}
}
```

### Fields

- `id`: `string`
- `type`: `string`
- `path`: `string[]`
- `parent_id`: `string | null`
- `depth`: `integer`
- `page_number`: `integer | null`
- `source_ref`: `SourceRef | null`
- `is_inferred`: `boolean`
- `chunk_hint`: `boolean` — present only when `chunkHints=true`
- `text`: `string | null`
- `attributes`: `object`

### Rules

- `id` must be deterministic.
- `type` must preserve the original emitted semantic block type.
- `path` is the canonical structural path.
- `parent_id` is `null` only for root blocks.
- `depth` must never be `null` in the public contract.
- `is_inferred` must never be `null` in the public contract.
- `page_number` remains a first-class field.
- `attributes` contains only type-specific fields not promoted into the universal base.
- `chunk_hint` must be omitted entirely when `chunkHints=false` or when the option is not supplied.

---

## 7. SourceRef

```json
{
  "page": 1,
  "sheet": "Sheet1",
  "row_index": 12,
  "column_index": 3,
  "slide_number": null,
  "address": "C12",
  "range_address": null,
  "name": null
}
```

### Fields

- `page`: `integer | null`
- `sheet`: `string | null`
- `row_index`: `integer | null`
- `column_index`: `integer | null`
- `slide_number`: `integer | null`
- `address`: `string | null`
- `range_address`: `string | null`
- `name`: `string | null`

### Rules

- `source_ref` is the canonical normalized location/provenance object.
- Location fields already present in `source_ref` must not be duplicated inside `attributes`.
- The `source_ref.name` field carries provenance (e.g., bookmark name, slide title, author) rather than content; identical content in `source_ref.name` and the `text` field is not considered a violation of the no-duplication rule.
- The orchestrator may either:
  - emit only populated fields, or
  - normalize all `SourceRef` fields with explicit nulls

Pick one and keep it consistent.

---

## 8. Block Type Naming

Block types should preserve the original semantic names emitted by the parser.

### Rule

- Do not rename block types unless required
- Do not collapse semantically distinct types
- Preserve prefixes like `word_`, `excel_`, and `ppt_` where they prevent ambiguity

---

## 9. Deterministic `text` Mapping Rule

The public contract includes a universal `text` field for downstream search, RAG, and GenAI consumption.

Because parser block models are heterogeneous, the orchestrator must populate `text` using the following deterministic priority order:

1. `Text`
2. `TextToDisplay`
3. `Code`
4. `Formula`
5. `Value` (converted to string when needed)
6. `Title`
7. `ChartTitle`
8. If block has `Entries` or `Texts` (string list), join with newlines
9. `AltText`
10. `Name`
11. `FontName`
12. If block is a `list`, concatenate item texts with newlines
13. `null`

### Rules

- `text` is the canonical downstream plain-text projection.
- **Empty or whitespace-only results at any priority step are treated as unmatched; the orchestrator must proceed to the next priority in the chain.**
- When `text` is derived from a non-`Text` property, the original structured field may remain in `attributes` only if it adds semantic value beyond the plain-text projection.
- Do not duplicate identical content in both `text` and `attributes` unless the structured field is semantically distinct.

---

## 10. Attributes

`attributes` is a format-specific object containing additional structured fields for the block.

This keeps the public contract compact while preserving fidelity.

### Rules

- `attributes` should always be present.
- Use `{}` when no additional type-specific fields exist.
- `attributes` must contain only fields relevant to the current block type.
- `attributes` must preserve original structured values where meaningful.
- `attributes` must not repeat normalized location fields already present in `source_ref`.
- `attributes` must not repeat the canonical `text` value unless the original field is semantically distinct.

### Canonical examples

- Keep `level` and `style_name` in `attributes` for a heading.
- Keep `evaluated_value` and typed result fields in `attributes` for a formula.
- Keep `items` in `attributes` for a list.
- Keep `Title` in `attributes` for content controls where meaningful (Title is the label, Text is the content).
- Keep comment metadata in `attributes` for comment blocks.
- Keep image, chart, shape, and table metadata in `attributes`.

---

## 11. Content Model

`content` is always an ordered array of blocks using the universal base contract.

Format-specific details must go inside `attributes`.

### Supported format families

#### Word

Examples of block `type` values:

- `word_metadata`
- `section`
- `heading`
- `paragraph`
- `list`
- `table`
- `table_row`
- `table_cell`
- `image`
- `header_footer`
- `bookmark`
- `bookmark_end`
- `word_comment`
- `word_comment_mark`
- `word_content_control`
- `word_form_field`
- `word_shape`
- `footnote`
- `endnote`
- `toc`
- `field`
- `hyperlink`
- `word_formula`
- `word_chart`
- `word_smartart`
- `page_break`

#### Excel

Examples of block `type` values:

- `workbook_metadata`
- `worksheet`
- `worksheet_row`
- `worksheet_column`
- `spreadsheet_row`
- `spreadsheet_cell`
- `cell_value`
- `cell_formula`
- `cell_formatting`
- `cell_comment`
- `excel_comment_thread`
- `excel_comment_reply`
- `named_range`
- `data_connection`
- `merged_cell`
- `data_validation`
- `conditional_formatting`
- `filter`
- `freeze_pane`
- `pivot_table`
- `pivot_field`
- `pivot_data_field`
- `excel_table`
- `table_row`
- `table_cell`
- `excel_image`
- `excel_shape`
- `excel_chart`
- `excel_smartart`
- `macro`
- `sparkline`
- `pivot_chart`
- `excel_hyperlink`

#### PowerPoint

Examples of block `type` values:

- `presentation_metadata`
- `slide`
- `slide_section`
- `slide_notes`
- `slide_comment`
- `slide_background`
- `slide_timing`
- `ppt_theme`
- `slide_master`
- `slide_layout`
- `heading`
- `image`
- `ppt_shape`
- `ppt_placeholder`
- `ppt_group_shape`
- `ppt_table`
- `ppt_table_row`
- `ppt_table_cell`
- `ppt_paragraph`
- `ppt_list`
- `ppt_text_run`
- `ppt_text_block`
- `ppt_hyperlink`
- `ppt_image`
- `ppt_video`
- `ppt_audio`
- `ppt_embedded_object`
- `ppt_chart`
- `ppt_smartart`
- `ppt_connector`
- `ppt_position`
- `ppt_layout_info`
- `ppt_alignment`
- `ppt_zindex`
- `ppt_animation`
- `ppt_transition`
- `ppt_action_link`
- `ppt_embedded_font`
- `handout_layout`

#### Web/Text

Examples of block `type` values:

- `heading`
- `paragraph`
- `list`
- `code`
- `blockquote`
- `horizontal_rule`
- `hyperlink`
- `image`

For `html` and `htm`, output may reuse the Word-family block model.

---

## 12. Fixed Compact Examples

### Word heading

```json
{
  "id": "blk_0001",
  "type": "heading",
  "path": ["Section 1"],
  "parent_id": null,
  "depth": 0,
  "page_number": 1,
  "source_ref": { "page": 1 },
  "is_inferred": false,
  "chunk_hint": true,
  "text": "Coverage",
  "attributes": {
    "level": 1,
    "style_name": "Heading 1"
  }
}
```

### Word comment

```json
{
  "id": "blk_cmt_001",
  "type": "word_comment",
  "path": ["Section 1"],
  "parent_id": "blk_0001",
  "depth": 1,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": "Please review this section.",
  "attributes": {
    "author": "Jane Smith",
    "initials": "JS",
    "created_time": "2025-01-10T09:15:00Z",
    "is_resolved": false,
    "done": false,
    "parent_para_id": null
  }
}
```

### Spreadsheet cell

```json
{
  "id": "blk_cell_001",
  "type": "spreadsheet_cell",
  "path": ["Sheet1", "Row 1"],
  "parent_id": "blk_row_001",
  "depth": 2,
  "page_number": null,
  "source_ref": {
    "sheet": "Sheet1",
    "row_index": 1,
    "column_index": 1,
    "address": "A1"
  },
  "is_inferred": false,
  "chunk_hint": false,
  "text": "Name",
  "attributes": {
    "column": "A",
    "data_type": "text",
    "numeric_value": null,
    "boolean_value": null,
    "date_value": null,
    "formula": null
  }
}
```

### Spreadsheet formula

```json
{
  "id": "blk_formula_001",
  "type": "cell_formula",
  "path": ["Sheet1", "Row 2"],
  "parent_id": "blk_cell_002",
  "depth": 3,
  "page_number": null,
  "source_ref": {
    "sheet": "Sheet1",
    "row_index": 2,
    "column_index": 2,
    "address": "B2"
  },
  "is_inferred": false,
  "text": "=SUM(B3:B10)",
  "attributes": {
    "evaluated_value": "6",
    "result_type": "number",
    "result_numeric_value": 6
  }
}
```

### PowerPoint slide

```json
{
  "id": "blk_slide_001",
  "type": "slide",
  "path": ["Slide 1"],
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": { "slide_number": 1, "name": "Q3 Results" },
  "is_inferred": false,
  "chunk_hint": true,
  "text": "Q3 Results",
  "attributes": {
    "master_name": "Office Theme",
    "layout_name": "Title Slide",
    "section_name": ""
  }
}
```

### Markdown code block

```json
{
  "id": "blk_code_001",
  "type": "code",
  "path": ["Section 2"],
  "parent_id": null,
  "depth": 1,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "chunk_hint": true,
  "text": "def hello():\n    return 'world'",
  "attributes": {
    "language": "python"
  }
}
```

### List block

```json
{
  "id": "blk_list_001",
  "type": "list",
  "path": ["Section 1"],
  "parent_id": null,
  "depth": 1,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "text": "First item\nNested item",
  "attributes": {
    "is_ordered": true,
    "start_number": 1,
    "items": [
      { "label": "1.", "level": 0, "text": "First item" },
      { "label": "-", "level": 1, "text": "Nested item" }
    ]
  }
}
```

### Name-only block example

```json
{
  "id": "blk_ws_001",
  "type": "worksheet",
  "path": ["Sheet1"],
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": { "sheet": "Sheet1" },
  "is_inferred": false,
  "text": "Sheet1",
  "attributes": {
    "visibility": "Visible",
    "is_protected": false
  }
}
```

---

## 13. Chunk Hint Behavior

### Request behavior

- `chunkHints` defaults to `false`
- `chunkHints=true` → eligible blocks may include `chunk_hint`
- `chunkHints=false` → `chunk_hint` must be omitted from all blocks

### Rule

The internal parser output may always compute chunk-boundary candidates, but the public contract exposes them only when requested.

---

## 14. Invariants

The final JSON output must preserve these invariants:

- **Linear order preserved** — output ordering must follow source reading or structural order
- **Deterministic output** — same source input must produce identical output
- **Truthful extraction** — no summarization, paraphrasing, rewriting, or semantic invention
- **Structural fidelity** — hierarchy must remain explicit via `path`, `parent_id`, and `depth`
- **Stable identifiers** — IDs must be deterministic and stable
- **Source traceability** — each meaningful block must be traceable via `source_ref` when available
- **Inference transparency** — any inferred structure must be marked with `is_inferred=true`
- **Warning visibility** — warnings must be included in final output
- **No synthetic filler** — output must not invent empty grouping blocks or meaningless wrappers

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

### SourceRef

Choose one strategy and keep it consistent:

- emit only populated fields, or
- normalize all fields to explicit nulls

### Attributes

- `attributes` should always be present
- use `{}` when no additional type-specific fields exist

---

## 16. Minimal Full Example

```json
{
  "schema_version": "1.0",
  "document": {
    "source_file": "sample.docx",
    "format": "docx",
    "format_family": "word",
    "title": "Insurance Policy",
    "author": "Jane Smith",
    "subject": "Coverage Terms",
    "company": null,
    "created_at": null,
    "modified_at": null,
    "page_count": null,
    "sheet_count": null,
    "slide_count": null
  },
  "warnings": [],
  "content": [
    {
      "id": "blk_0001",
      "type": "heading",
      "path": ["Section 1"],
      "parent_id": null,
      "depth": 0,
      "page_number": 1,
      "source_ref": { "page": 1 },
      "is_inferred": false,
      "chunk_hint": true,
      "text": "Coverage",
      "attributes": {
        "level": 1,
        "style_name": "Heading 1"
      }
    },
    {
      "id": "blk_0002",
      "type": "paragraph",
      "path": ["Section 1"],
      "parent_id": "blk_0001",
      "depth": 1,
      "page_number": 1,
      "source_ref": { "page": 1 },
      "is_inferred": false,
      "chunk_hint": false,
      "text": "This policy covers accidental damage.",
      "attributes": {
        "style_name": "Normal"
      }
    }
  ]
}
```

---

## 17. Implementation Notes for Orchestrator

The orchestrator must:

- derive `format_family` from `format`
- preserve stable IDs, hierarchy, warnings, and inference markers
- preserve `page_number` as a first-class base field
- preserve or normalize `source_ref`
- hoist `subject` and `company` from metadata blocks where present
- compute `sheet_count` by counting `worksheet` blocks if exposed
- compute `slide_count` by counting `slide` blocks if exposed
- default unsupported metadata such as `created_at` and `modified_at` to `null`
- expose `chunk_hint` only when `chunkHints=true`
- preserve original block type names without renaming
- map type-specific fields into `attributes` without dropping meaningful semantics
- apply the deterministic `text` mapping rule consistently
- ensure `depth` and `is_inferred` are never null in the public contract
- derive `source_ref` for `cell_formula` (sheet, indices, address) from parent block context or by parsing the `Address` property where missing in the internal output
- derive `source_ref.slide_number` for `slide` blocks from the `SlideNumber` property where missing in the internal output

---

## 18. Versioning

| Version | Change                                                               |
| ------- | -------------------------------------------------------------------- |
| `1.0`   | Initial compact public output JSON contract for MS Office + Web/Text |
````
