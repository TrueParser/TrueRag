# TrueParser — Compact Output JSON Contract Specification (PDF)
# For implementing thsi spec you should update the pdf package to TrueParserPdf --version 0.4.3

**Version**: 1.0  
**Scope**: PDF  
**Input formats**: `pdf`

**Use cases**: Analytics · Search · RAG · Generative AI Pipelines
## PDF package to use for this ocntract: TrueParserPdf --version 0.4.3

# Important: this is not provided by PDF package, this is orchestrator job. To consume the NDjson contract and produce ourput json with this spec.

---

## 1. Purpose

This specification defines the **final JSON output contract** returned to consumers for PDF documents parsed by TrueParser.

The PDF parsing engine emits internal NDJSON streams for transport between the engine and the orchestration layer. Those internal NDJSON streams are **not** part of the consumer-facing contract. The orchestration layer is responsible for consuming the internal PDF NDJSON and producing the final compact JSON defined here.

This compact contract is designed to be:

- structured
- stable
- high-fidelity
- easier for downstream search, analytics, RAG, and GenAI pipelines to consume than raw NDJSON

---

## 2. Design Principles

- **Final JSON is the consumer contract**
- **Compact but high-fidelity**
- **Stable universal block shape**
- **Format-specific PDF details live under `attributes`**
- **Hierarchy and provenance are preserved**
- **Spatial information is preserved**
- **Chunk hints are request-controlled at the orchestrator layer**
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

### Fields

- `schema_version` — string, always `"1.0"`
- `document` — document-level metadata
- `warnings` — array of warning objects
- `content` — ordered array of extracted content blocks

### Rule

The internal PDF NDJSON does not emit a top-level warnings array. If the public contract exposes `warnings`, the orchestration layer must synthesize it from non-fatal notes or other processing signals when needed. Otherwise it should emit an empty array. Internally, non-fatal conditions are typically carried in metadata notes.

---

## 4. Document

```json
{
  "source_file": "policy.pdf",
  "format": "pdf",
  "format_family": "pdf",
  "title": "Insurance Policy",
  "author": null,
  "subject": null,
  "company": null,
  "created_at": null,
  "modified_at": null,
  "page_count": 10,
  "source_mode": "basic",
  "source_engine": "TrueParserPdf.Basic"
}
```

### Fields

- `source_file`: `string | null`
- `format`: `string`
- `format_family`: `"pdf"`
- `title`: `string | null`
- `author`: `string | null`
- `subject`: `string | null`
- `company`: `string | null`
- `created_at`: `string | null`
- `modified_at`: `string | null`
- `page_count`: `integer | null`
- `source_mode`: `string | null`
- `source_engine`: `string | null`

### Rules

- `format` is always `"pdf"`.
- `format_family` is always `"pdf"`.
- **Field Availability Warning**: The core PDF engine does not extract document properties (title, author, subject, etc.) from the PDF file itself. These fields will be `null` unless the orchestrator provides them from an external source or provider-specific metadata.
- `title` is only populated if the underlying provider (e.g., Gemini) includes it in its metadata dictionary.
- `author`, `subject`, `company`, `created_at`, and `modified_at` are **not** populated by any current PDF pipeline and should be considered reserved/external.
- `page_count` is not present in the canonical NDJSON header; it must be derived from the lossless `doc` record metadata or provided by the orchestration context.
- `source_mode` and `source_engine` are hoisted from block-level records.

---

## 5. Warnings

```json
{
  "code": "PDF_EXTRACTION_NOTE",
  "message": "Non-fatal extraction note emitted during PDF normalization.",
  "source": "TrueParserPdf"
}
```

### Fields

- `code`: `string`
- `message`: `string`
- `source`: `string | null`

### Rules

- `warnings` is always present at the top level.
- If there are no warnings, emit `[]`.
- The orchestration layer may map internal metadata notes or normalization issues into warning records when exposing them publicly.

---

## 6. Universal Base Block Contract

Every meaningful extracted unit in `content` must use this base shape.

```json
{
  "id": "p1-o3-paragraph",
  "type": "paragraph",
  "path": ["Introduction", "Coverage"],
  "parent_id": "p1-o1-paragraph",
  "depth": 2,
  "page_number": 1,
  "order": 3,
  "bbox": {
    "x": 72.0,
    "y": 100.0,
    "width": 468.0,
    "height": 14.0
  },
  "source_ref": {
    "page": 1,
    "block_id": "p1-o3-paragraph",
    "source_mode": "basic",
    "source_engine": "TrueParserPdf.Basic"
  },
  "is_inferred": false,
  "chunk_hint": true,
  "text": "This policy covers accidental damage.",
  "attributes": {}
}
```

### Fields

- `id`: `string`
- `type`: `string`
- `path`: `string[]`
- `parent_id`: `string | null`
- `depth`: `integer`
- `page_number`: `integer`
- `order`: `integer`
- `bbox`: `BBox`
- `source_ref`: `SourceRef | null`
- `is_inferred`: `boolean`
- `chunk_hint`: `boolean` — present only when `chunkHints=true`; omitted by default when `chunkHints=false` or not supplied
- `text`: `string | null`
- `attributes`: `object`

### Rules

- `id` should come from canonical `BlockId` when available.
- `type` should preserve the resolved rich semantic block type.
- `path` should come from `SectionPath` when present, else `[]`.
- `parent_id` should come from `ParentId` when present.
- `depth` represents the structural section hierarchy depth (how many heading levels deep the block is). It is derived from `path.length`. It does **not** represent parent-child nesting depth; use `parent_id` for explicit parent-child relationships.
- `page_number` must come from `PageNumber`.
- `order` must come from canonical `Order`.
- `bbox` must come from canonical `BBox`.
- `source_ref` is the canonical normalized provenance/location object.
- `is_inferred` is a synthetic field derived by the orchestration layer. It must be `true` only when the block's resolved type was changed by semantic enrichment (e.g., a paragraph promoted to a `heading`, `list`, or `header_footer`) or if it was classified as a `caption`. Plain paragraphs and tables that merely received metadata annotations should have `is_inferred = false`.
- `attributes` contains type-specific PDF fields not promoted into the universal base.

---

## 7. BBox

```json
{
  "x": 72.0,
  "y": 100.0,
  "width": 468.0,
  "height": 14.0
}
```

### Fields

- `x`: `number`
- `y`: `number`
- `width`: `number`
- `height`: `number`

### Rules

- `bbox` is always required on public content blocks.
- Bounding box coordinates use normalized PDF-space geometry.
- Width and height must be non-negative.

---

## 8. SourceRef

```json
{
  "page": 1,
  "block_id": "p1-o3-paragraph",
  "source_mode": "basic",
  "source_engine": "TrueParserPdf.Basic"
}
```

### Fields

- `page`: `integer | null`
- `block_id`: `string | null`
- `source_mode`: `string | null`
- `source_engine`: `string | null`

### Rules

- `source_ref` is the canonical normalized provenance object for public consumers.
- `source_mode` must be one of: `"basic"`, `"singleColumn"`, `"multiColumn"`, `"ocr"`, or `"advanced"`.
- It should contain only normalized location/provenance fields.
- Fields already present in `source_ref` must not be duplicated inside `attributes`.
- The orchestrator may either emit only populated fields or normalize all fields with explicit nulls, but it must be consistent.

---

## 9. Allowed Block Types

The public PDF contract supports these resolved block types:

- `paragraph`
- `heading`
- `list`
- `table`
- `image`
- `header_footer`

**Note**: Paragraphs can be semantically enriched as **captions** (carrying `semantic_kind: "caption"`). These remain of type `paragraph` but are structurally linked to tables or images.

These are the rich semantic types resolved by the canonical PDF pipeline.

---

## 10. Deterministic `text` Mapping Rule

The public contract includes a universal `text` field for downstream search, RAG, analytics, and GenAI consumption.

For PDF blocks, the orchestration layer must populate `text` using the following deterministic priority order:

1. If the block is an `image` and has `AltText`, use `AltText`.
2. Else if the block has `Text`, use it.
3. Else if the block has `Markdown`, use it.
4. Else if the block is a `list`, concatenate item texts joined with newline characters.
5. Else if the block has table rows or cells, use the canonical table text representation.
6. Else if the block has `FileName`, use it.
7. Else `text = null`.

### Rules

- `text` is the canonical downstream plain-text projection.
- Do not duplicate identical content in both `text` and `attributes` unless the structured field is semantically distinct.
- For list blocks, `text` is a derived plain-text projection; structured items remain in `attributes.items`.
- For image blocks, `AltText` is preferred over file-oriented identifiers. If `AltText` is absent, `text` will contain the technical image identifier (e.g., `"img-0.jpeg"`) from the source `Text` field.
- For table blocks, `text` should use the normalized tab/newline textual rendering already present in canonical output.

---

## 11. Note on Normalization and Compactness

This contract is intentionally **normalized and compact**.

The goal of the final output JSON is not to mirror every native PDF parser field as a top-level property on every block. Instead, it provides a stable universal block shape for downstream consumers while preserving PDF-specific fidelity in a controlled way.

### Normalization

The orchestration layer normalizes heterogeneous PDF block models into a consistent public structure:

- common fields such as `id`, `type`, `path`, `parent_id`, `depth`, `page_number`, `order`, `bbox`, `source_ref`, and optional `chunk_hint` are promoted into the universal base block contract, while `is_inferred` is synthesized based on enrichment markers
- `text` is populated using deterministic text-mapping rules
- `source_ref` is the canonical normalized provenance object
- block type names are preserved as resolved rich semantic types
- semantic structure such as section hierarchy and parent linkage is retained

### Compactness

The contract is also intentionally **compact**.

Rather than expanding every PDF-specific field into the universal block shape, type-specific details are placed under `attributes`. This keeps the public schema:

- easier to understand
- easier to index
- easier to consume in search, analytics, RAG, and GenAI pipelines
- more stable over time as parser fields evolve

Compactness does **not** mean lossy simplification. It means:

- promote only fields that are universally important for downstream use
- keep structured type-specific fidelity in `attributes`
- avoid redundant duplication across `text`, `source_ref`, and `attributes`

### 11.1 Canonical to Compact Mapping

The following table defines how the orchestrator must map internal canonical fields (emitted as top-level PascalCase) into the final compact JSON (emitted as snake_case).

| Internal Canonical Field     | Public Placement | Public Field Name      | Note                                                                                                   |
| :--------------------------- | :--------------- | :--------------------- | :----------------------------------------------------------------------------------------------------- |
| `BlockId`                    | Base             | `id`                   |                                                                                                        |
| `Type`                       | Base             | `type`                 | Resolved rich semantic type                                                                            |
| `SectionPath`                | Base             | `path`                 | Always present as array; identical to `Breadcrumbs`                                                    |
| `ParentId`                   | Base             | `parent_id`            |                                                                                                        |
| `PageNumber`                 | Base             | `page_number`          |                                                                                                        |
| `Order`                      | Base             | `order`                |                                                                                                        |
| `BBox`                       | Base             | `bbox`                 |                                                                                                        |
| `Text`                       | Base             | `text`                 | Subject to [Mapping Rule 10](#10-deterministic-text-mapping-rule)                                      |
| `IsChunkBoundaryCandidate`   | Base             | `chunk_hint`           | Conditional on `chunkHints=true`                                                                       |
| `Role`                       | `attributes`     | `role`                 | Mapped to `body`, `header`, `footer`, or `title`; `Unknown` is mapped to `body`                        |
| `Confidence`                 | `attributes`     | `confidence`           |                                                                                                        |
| `Lines`                      | `attributes`     | `lines`                |                                                                                                        |
| `Hyperlinks`                 | `attributes`     | `hyperlinks`           |                                                                                                        |
| `CharCount`                  | `attributes`     | `char_count`           |                                                                                                        |
| `TokenCountEstimate`         | `attributes`     | `token_count_estimate` |                                                                                                        |
| `Language`                   | `attributes`     | `language`             |                                                                                                        |
| `ContentHash`                | -                | -                      | Only available in internal NDJSON; not projected.                                                      |
| `Metadata.sourceSubsystem`   | `attributes`     | `source_subsystem`     | Engine provenance                                                                                      |
| `Metadata.regionId`          | `attributes`     | `region_id`            | Unique region identifier                                                                               |
| `Metadata.notes`             | `attributes`     | `notes`                | Extra extraction notes                                                                                 |
| `Metadata.attributes.*`      | `attributes`     | `(snake_case)`         | Flattened semantic enrichment (e.g., `semantic_kind`). **Note**: Values remain string-encoded.         |
| Level                        | `attributes`     | `level`                | Emitted on ANY block with semantic nesting                                                             |
| Cells                        | `attributes`     | `cells`                | Emitted internally for all blocks, but only populated for tables                                       |
| `Data` (projected as `Rows`) | `attributes`     | `rows`                 | Table-specific                                                                                         |
| `AltText`                    | `attributes`     | `alt_text`             | Image-specific                                                                                         |
| `Src`                        | `attributes`     | `src`                  | Image-specific                                                                                         |
| `FileName`                   | `attributes`     | `file_name`            | Image-specific                                                                                         |
| `Base64`                     | `attributes`     | `base64`               | Image-specific                                                                                         |
| `Width`                      | `attributes`     | `width`                | Image-specific                                                                                         |
| `Height`                     | `attributes`     | `height`               | Image-specific                                                                                         |
| `HasPageNumber`              | -                | -                      | Omit from output (always `true` for PDF)                                                               |
| `captionForBlockId`          | `attributes`     | `caption_for_block_id` | Caption paragraph → table link                                                                         |
| `captionBlockId`             | `attributes`     | `caption_block_id`     | Table → caption paragraph link                                                                         |
| `captionText`                | `attributes`     | `caption_text`         | Table-side copy of caption text                                                                        |
| `TableCell.*`                | `attributes`     | `cells`                | Map internal `row`, `col`, `rowSpan`, `colSpan` to `row_index`, `column_index`, `row_span`, `col_span` |
| `semantic_origin`            | `attributes`     | `semantic_origin`      | Always `"inferred"` for enriched blocks                                                                |
| `semantic_enriched`          | `attributes`     | `semantic_enriched`    | Always `"true"` (string) for enriched blocks                                                           |
| `semantic_version`           | `attributes`     | `semantic_version`     | Always `"1"` (string)                                                                                  |
| `semantic_page`              | `attributes`     | `semantic_page`        | Page identifier (primarily tables)                                                                     |

---

## 12. Attributes

`attributes` is a PDF-specific object containing additional structured fields for the block.

### Rules

- `attributes` should always be present.
- Use `{}` when no additional type-specific fields exist.
- `attributes` must contain only fields relevant to the current block type.
- `attributes` must preserve original structured values where meaningful.
- `attributes` must not repeat normalized provenance/location fields already present in `source_ref`.
- `attributes` must not repeat the canonical `text` value unless the original field is semantically distinct.
- **Flattening Rule**: Fields from the internal `Metadata` object (including its inner `attributes` dictionary) must be flattened into snake_case keys directly under the public `attributes` object (e.g., `sourceSubsystem` becomes `source_subsystem`, `semanticKind` becomes `semantic_kind`) to avoid deep nesting.
- **Type Handling Note**: Flattened metadata attribute values retain their original string type from the internal dictionary. Orchestrator-computed fields (like `level` or `row_count`) are emitted as integers derived by parsing the raw internal data.
- **Redundancy Omission**: The orchestrator should omit `has_page_number` from public output since it is always true for PDF blocks.

### Typical attribute groups

- semantic metadata
- confidence
- hyperlinks
- language
- token/count estimates
- list item structure
- table structure
- image metadata
- provider/projection notes

---

## 13. Type-Specific Attribute Shapes

### 13.1 Paragraph

```json
{
  "confidence": 0.92,
  "role": "body",
  "level": 1,
  "char_count": 42,
  "token_count_estimate": 8,
  "language": "en",
  "hyperlinks": ["https://example.com"],
  "lines": ["This policy covers accidental damage."],
  "source_subsystem": "Syncfusion",
  "region_id": "p1-paragraph-3",
  "notes": null,
  "semantic_kind": "body"
}
```

- `level`: `integer | null`. Indicates semantic nesting depth (e.g., within a list or section hierarchy). May appear on **any** enriched block type.
- `semantic_origin`: `string`. Always `"inferred"` for blocks modified by the semantic pipeline.
- `semantic_enriched`: `string`. Always `"true"` for blocks modified by the semantic pipeline.
- `semantic_version`: `string`. Internal version of the enricher (typically `"1"`).
- `semantic_page`: `string`. Document-relative page index (principally for tables).
- `char_count`: `integer`. Count of characters in `text`.
- `token_count_estimate`: `integer`. Estimated tokens for LLM contexts.
- `language`: `string | null`. ISO language code if detected.
- `hyperlinks`: `string[] | null`. List of URLs found in the block.
- `lines`: `string[]`. Raw line-separated text before normalizations.
- `source_subsystem`: `string`. The engine component that produced the block.
- `region_id`: `string`. A stable, unique identifier for the original bounding region.
- `notes`: `string | null`. Additional extraction metadata or warnings.
- `semantic_kind`: `string | null`. The rich semantic role assigned (e.g., `body`, `caption`).

#### Caption (semantically enriched paragraph)

```json
{
  "confidence": 0.94,
  "role": "body",
  "source_subsystem": "Syncfusion",
  "region_id": "p5-paragraph-7",
  "semantic_kind": "caption",
  "caption_for_block_id": "p5-o8-table"
}
```

### 13.2 Heading

```json
{
  "level": 1,
  "confidence": 0.96,
  "role": "title",
  "char_count": 17,
  "token_count_estimate": 3,
  "language": "en",
  "hyperlinks": null,
  "lines": ["EXECUTIVE SUMMARY"],
  "source_subsystem": "Syncfusion",
  "region_id": "p1-paragraph-1",
  "notes": null,
  "semantic_kind": "heading",
  "semantic_level": "1",
  "semantic_depth": "1",
  "semantic_origin": "inferred",
  "semantic_enriched": "true",
  "semantic_version": "1"
}
```

### 13.3 Header/Footer

```json
{
  "kind": "footer",
  "variant": "default",
  "confidence": 0.99,
  "role": "footer",
  "source_subsystem": "Syncfusion",
  "region_id": "p3-footer-1",
  "notes": null
}
```

### 13.4 List

```json
{
  "is_ordered": true,
  "start_number": 1,
  "items": [
    {
      "label": "1.",
      "level": 1,
      "text": "1. First item",
      "markdown": "1. First item"
    }
  ],
  "confidence": 0.91,
  "role": "body",
  "source_subsystem": "Syncfusion",
  "region_id": "p2-list-5",
  "notes": null,
  "list_group_id": "p2-list-5",
  "list_item_index": "0",
  "list_kind": "ordered"
}
```

> [!NOTE]
> **List Attributes**: It is a design limitation of the current projection writer that `start_number` is fixed at `1` for all ordered lists. Additionally, `markdown` in list items currently mirrors the `text` field for PDF output.
>
> **List Aggregation**: The PDF engine emits list blocks on a per-item basis. Consequently, the `items` array will always contain exactly **one** element. Consumers wishing to reconstruct full lists must aggregate sequential blocks that share the same `list_group_id`.

### 13.5 Table

```json
{
  "rows": [
    ["Name", "Rate"],
    ["Clark County", "6.50%"]
  ],
  "cells": [
    {
      "row_index": 0,
      "column_index": 0,
      "text": "Name",
      "row_span": null,
      "col_span": null
    },
    {
      "row_index": 0,
      "column_index": 1,
      "text": "Rate",
      "row_span": null,
      "col_span": null
    }
  ],
  "row_count": 2,
  "column_count": 2,
  "header_row_count": 1,
  "confidence": 0.95,
  "role": "body",
  "source_subsystem": "TabulaSharp",
  "region_id": "p5-table-1",
  "notes": null,
  "semantic_kind": "table",
  "caption_block_id": "p5-paragraph-7",
  "caption_text": "Table 1: Tax Rates by County",
  "semantic_page": "5"
}
```

### 13.6 Image

```json
{
  "alt_text": null,
  "src": null,
  "file_name": null,
  "base64": "data:image/jpeg;base64,/9j/4AAQ...",
  "width": 1573,
  "height": 1771,
  "confidence": null,
  "role": "body",
  "source_subsystem": "MistralOcr",
  "region_id": "p1-image-1",
  "notes": null
}
```

> [!NOTE]
> Enrichment fields are omitted from some Section 13 shapes for brevity; see Section 14 for complete examples.

These type-specific fields are grounded in the canonical PDF block contract.

---

## 14. Compact Examples

### Heading

```json
{
  "id": "p1-o1-paragraph",
  "type": "heading",
  "path": ["EXECUTIVE SUMMARY"],
  "parent_id": null,
  "depth": 1,
  "page_number": 1,
  "order": 1,
  "bbox": { "x": 72, "y": 50, "width": 200, "height": 16 },
  "source_ref": {
    "page": 1,
    "block_id": "p1-o1-paragraph",
    "source_mode": "basic",
    "source_engine": "TrueParserPdf.Basic"
  },
  "is_inferred": true,
  "chunk_hint": true,
  "text": "EXECUTIVE SUMMARY",
  "attributes": {
    "level": 1,
    "role": "title",
    "confidence": null,
    "source_subsystem": "Syncfusion",
    "region_id": "p1-paragraph-1",
    "notes": null,
    "semantic_kind": "heading",
    "semantic_level": "1",
    "semantic_depth": "1",
    "semantic_path": "EXECUTIVE SUMMARY",
    "semantic_origin": "inferred",
    "semantic_enriched": "true",
    "semantic_version": "1"
  }
}
```

### Paragraph

```json
{
  "id": "p1-o2-paragraph",
  "type": "paragraph",
  "path": ["EXECUTIVE SUMMARY"],
  "parent_id": "p1-o1-paragraph",
  "depth": 1,
  "page_number": 1,
  "order": 2,
  "bbox": { "x": 72, "y": 100, "width": 468, "height": 14 },
  "source_ref": {
    "page": 1,
    "block_id": "p1-o2-paragraph",
    "source_mode": "basic",
    "source_engine": "TrueParserPdf.Basic"
  },
  "is_inferred": false,
  "chunk_hint": false,
  "text": "This policy covers accidental damage.",
  "attributes": {
    "role": "body",
    "confidence": null,
    "char_count": 37,
    "token_count_estimate": 6,
    "language": "en",
    "lines": ["This policy covers accidental damage."],
    "source_subsystem": "Syncfusion",
    "region_id": "p1-paragraph-2",
    "semantic_kind": "body",
    "semantic_level": "1",
    "semantic_depth": "1",
    "semantic_path": "EXECUTIVE SUMMARY > This policy covers accidental damage.",
    "semantic_origin": "inferred",
    "semantic_enriched": "true",
    "semantic_version": "1"
  }
}
```

### List

```json
{
  "id": "p2-o5-paragraph",
  "type": "list",
  "path": ["Benefits"],
  "parent_id": "p2-o1-paragraph",
  "depth": 1,
  "page_number": 2,
  "order": 5,
  "bbox": { "x": 90, "y": 200, "width": 400, "height": 12 },
  "source_ref": {
    "page": 2,
    "block_id": "p2-o5-paragraph",
    "source_mode": "basic",
    "source_engine": "TrueParserPdf.Basic"
  },
  "is_inferred": true,
  "chunk_hint": true,
  "text": "1. First item",
  "attributes": {
    "is_ordered": true,
    "start_number": 1,
    "items": [
      {
        "label": "1.",
        "level": 1,
        "text": "1. First item",
        "markdown": "1. First item"
      }
    ],
    "list_kind": "ordered",
    "semantic_kind": "list-item",
    "list_group_id": "p2-list-5",
    "list_item_index": "0",
    "semantic_level": "1",
    "semantic_depth": "1",
    "semantic_path": "Benefits > 1. First item",
    "semantic_origin": "inferred",
    "semantic_enriched": "true",
    "semantic_version": "1"
  }
}
```

> [!NOTE]
> For PDF list items, `markdown` is currently a copy of the `text` field.

### Table

```json
{
  "id": "p5-o8-table",
  "type": "table",
  "path": ["Rates"],
  "parent_id": "p5-o1-paragraph",
  "depth": 1,
  "page_number": 5,
  "order": 8,
  "bbox": { "x": 72, "y": 300, "width": 468, "height": 80 },
  "source_ref": {
    "page": 5,
    "block_id": "p5-o8-table",
    "source_mode": "basic",
    "source_engine": "TrueParserPdf.Basic"
  },
  "is_inferred": false,
  "chunk_hint": true,
  "text": "Name\tRate\nClark County\t6.50%",
  "attributes": {
    "rows": [
      ["Name", "Rate"],
      ["Clark County", "6.50%"]
    ],
    "row_count": 2,
    "column_count": 2,
    "header_row_count": 1,
    "cells": [
      {
        "row_index": 0,
        "column_index": 0,
        "text": "Name",
        "row_span": null,
        "col_span": null
      },
      {
        "row_index": 0,
        "column_index": 1,
        "text": "Rate",
        "row_span": null,
        "col_span": null
      }
    ],
    "source_subsystem": "TabulaSharp",
    "region_id": "p5-table-1",
    "semantic_kind": "table",
    "semantic_level": "1",
    "semantic_depth": "1",
    "semantic_path": "Rates",
    "semantic_page": "5",
    "semantic_origin": "inferred",
    "semantic_enriched": "true",
    "semantic_version": "1"
  }
}
```

### Image

```json
{
  "id": "p1-o3-image",
  "type": "image",
  "path": [],
  "parent_id": null,
  "depth": 0,
  "page_number": 1,
  "order": 3,
  "bbox": { "x": 69, "y": 237, "width": 1573, "height": 1771 },
  "source_ref": {
    "page": 1,
    "block_id": "p1-o3-image",
    "source_mode": "ocr",
    "source_engine": "Mistral"
  },
  "is_inferred": false,
  "chunk_hint": false,
  "text": "img-0.jpeg",
  "attributes": {
    "alt_text": null,
    "base64": "data:image/jpeg;base64,/9j/4AAQ...",
    "width": 1573,
    "height": 1771
  }
}
```

### Header/Footer

```json
{
  "id": "p3-o15-paragraph",
  "type": "header_footer",
  "path": [],
  "parent_id": null,
  "depth": 0,
  "page_number": 3,
  "order": 15,
  "bbox": { "x": 72, "y": 750, "width": 468, "height": 10 },
  "source_ref": {
    "page": 3,
    "block_id": "p3-o15-paragraph",
    "source_mode": "basic",
    "source_engine": "TrueParserPdf.Basic"
  },
  "is_inferred": true,
  "chunk_hint": false,
  "text": "Page 3 of 10",
  "attributes": {
    "kind": "footer",
    "variant": "default",
    "semantic_kind": "footer",
    "semantic_origin": "inferred",
    "semantic_enriched": "true",
    "semantic_version": "1"
  }
}
```

---

## 15. Chunk Hint Behavior

### Request behavior

- `chunkHints` defaults to `false`
- `chunkHints=true` → include `chunk_hint` on every block
- `chunkHints=false` → omit `chunk_hint` from every block

### Rule

The internal canonical PDF contract always computes and emits `IsChunkBoundaryCandidate`. The public contract may expose it conditionally based on client request.

---

## 16. Invariants

The final JSON output must preserve these invariants:

- **Linear order preserved** — output ordering must follow page order and reading order within page
- **Deterministic output** — same input must produce identical output
- **Truthful extraction** — no summarization, paraphrasing, rewriting, or semantic invention
- **Structural fidelity** — hierarchy must remain explicit via `path`, `parent_id`, and `depth`
- **Stable identifiers** — IDs must be deterministic and stable
- **Source traceability** — each block must retain `page_number`, `bbox`, and `source_ref`
- **Inference transparency** — inferred semantics must be surfaced via `is_inferred = true` when the orchestrator detects that a block's type was promoted during enrichment (e.g., paragraph to heading).
- **No synthetic filler** — output must not invent meaningless grouping blocks
- **Compactness without loss** — type-specific fidelity must remain in `attributes`

These invariants align with the internal PDF NDJSON behavior and semantic enrichment pipeline.

---

## 17. Null and Omission Rules

### Required fields

Required fields must always be present:

- `schema_version`
- `document`
- `warnings`
- `content`
- block base fields except `chunk_hint` when disabled

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

### Note

The internal canonical PDF NDJSON omits null fields, but the public compact contract may normalize them if desired.

---

## 18. Minimal Full Example

```json
{
  "schema_version": "1.0",
  "document": {
    "source_file": "policy.pdf",
    "format": "pdf",
    "format_family": "pdf",
    "title": "Insurance Policy",
    "author": null,
    "subject": null,
    "company": null,
    "created_at": null,
    "modified_at": null,
    "page_count": 2,
    "source_mode": "basic",
    "source_engine": "TrueParserPdf.Basic"
  },
  "warnings": [],
  "content": [
    {
      "id": "p1-o1-paragraph",
      "type": "heading",
      "path": ["EXECUTIVE SUMMARY"],
      "parent_id": null,
      "depth": 1,
      "page_number": 1,
      "order": 1,
      "bbox": { "x": 72, "y": 50, "width": 200, "height": 16 },
      "source_ref": {
        "page": 1,
        "block_id": "p1-o1-paragraph",
        "source_mode": "basic",
        "source_engine": "TrueParserPdf.Basic"
      },
      "is_inferred": true,
      "chunk_hint": true,
      "text": "EXECUTIVE SUMMARY",
      "attributes": {
        "level": 1,
        "role": "title"
      }
    },
    {
      "id": "p1-o2-paragraph",
      "type": "paragraph",
      "path": ["EXECUTIVE SUMMARY"],
      "parent_id": "p1-o1-paragraph",
      "depth": 1,
      "page_number": 1,
      "order": 2,
      "bbox": { "x": 72, "y": 100, "width": 468, "height": 14 },
      "source_ref": {
        "page": 1,
        "block_id": "p1-o2-paragraph",
        "source_mode": "basic",
        "source_engine": "TrueParserPdf.Basic"
      },
      "is_inferred": false,
      "chunk_hint": false,
      "text": "This policy covers accidental damage.",
      "attributes": {
        "role": "body",
        "char_count": 37,
        "token_count_estimate": 6
      }
    }
  ]
}
```

---

## 19. Implementation Notes for Orchestrator

The orchestrator must:

- consume canonical PDF NDJSON as the source for public compact output
- derive `document` metadata from the header record and orchestration context
- preserve stable block IDs, hierarchy, order, and spatial geometry
- preserve normalized provenance in `source_ref`
- map resolved rich semantic block types directly into public `type`
- map PDF-specific fields into `attributes` without dropping meaningful semantics
- compute `depth` from `path`
- synthesize `is_inferred` by detecting type-promotions (e.g., if a paragraph was promoted to a heading or list item) or specific semantic kinds like `caption`.
- expose `chunk_hint` only when `chunkHints=true`
- preserve `page_number`, `order`, and `bbox` as first-class base fields

### Recommended source

For public compact output, prefer the **canonical rich semantic NDJSON** rather than the lossless provider NDJSON, because it already contains:

- resolved semantic block types
- heading/list/header-footer enrichment
- section hierarchy
- parent linkage
- normalized bounding boxes
- stable reading order.

---

## 20. Versioning

| Version | Change                                              |
| ------- | --------------------------------------------------- |
| `1.0`   | Initial compact public output JSON contract for PDF |

```

```
