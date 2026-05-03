# TrueParserCad - Output JSON Contract (CAD)

**Version**: 1.0  
**Scope**: CAD  
**Primary output**: pretty-printed JSON document  
**Stream mode**: NDJSON records are also supported by `ParseToStreamAsync`  
**Input formats**: `DWG`, `DXF`
**Package to use to implement this** :  TrueParserCad --version 0.2.1
---

## 1. Purpose

This document defines the **client-facing JSON contract** emitted by `TrueParserCad`.

The output is intended for downstream consumers that want a single structured JSON document containing:

- document-level metadata
- layers
- blocks
- block entities
- top-level entities
- drawing extents

The contract is designed to preserve source fidelity while still normalizing geometry and metadata into a predictable shape.

The JSON contract is separate from the internal NDJSON transport contract used by the parser pipeline.

---

## 2. Output Modes

`TrueParserCad` can emit output in two forms:

1. **Pretty-printed JSON document**
   - produced when `CadOutputFormat.Json` is requested
   - serialized from the in-memory `CadDocument`

2. **NDJSON stream**
   - produced by the default `ParseToStreamAsync` behavior
   - records are line-delimited JSON objects with `recordType`
   - this format is a transport shape, not the final materialized JSON document

This document focuses on the final JSON document while noting the NDJSON transport structure where it affects the contract.

---

## 3. Top-Level Document Shape

The materialized JSON document is a single object with these top-level fields:

- `format`
- `format_family`
- `document_name`
- `source_format`
- `document_format`
- `units`
- `source_units`
- `version`
- `title`
- `subject`
- `company`
- `schema_version`
- `schemaVersion`
- `author`
- `source_author`
- `created`
- `source_file`
- `source_engine`
- `metadata`
- `extents`
- `layer_count`
- `block_count`
- `block_entity_count`
- `entity_count`
- `layers`
- `blocks`
- `blockEntities`
- `entities`

### Example

```json
{
  "format": "DWG",
  "format_family": "cad",
  "document_name": "drawing.dwg",
  "source_format": "DWG",
  "document_format": "DWG",
  "units": "Millimeters",
  "source_units": "Millimeters",
  "version": "AC1032",
  "title": null,
  "subject": null,
  "company": null,
  "schema_version": "1.0",
  "schemaVersion": 1,
  "author": "Engineering Team",
  "source_author": "ACadSharp",
  "created": "2026-04-11T08:15:00Z",
  "source_file": "drawing.dwg",
  "source_engine": "TrueParserCad",
  "metadata": {},
  "extents": {
    "min": [0.0, 0.0],
    "max": [100.0, 50.0]
  },
  "layer_count": 32,
  "block_count": 41,
  "block_entity_count": 913,
  "entity_count": 2487,
  "layers": [],
  "blocks": [],
  "blockEntities": {},
  "entities": []
}
```

---

## 4. Document Fields

| Field | Type | Emitted shape | Notes |
| --- | --- | --- | --- |
| `format` | `string` | always present | Source format as normalized by the parser, such as `DWG` or `DXF` |
| `format_family` | `string` | always present | Always `cad` |
| `document_name` | `string` | always present | Defaults to `source_file` when available |
| `source_format` | `string` | always present | Original format label preserved for provenance |
| `document_format` | `string` | present when set, otherwise omitted | Mirrors the document format label |
| `units` | `string` | always present | Units normalized for consumer-facing output |
| `source_units` | `string` | present when set, otherwise omitted | Original unit label from the source file |
| `version` | `string` | always present | Source document version |
| `title` | `string` | always present, may be `null` | Reserved for future enrichment |
| `subject` | `string` | always present, may be `null` | Reserved for future enrichment |
| `company` | `string` | always present, may be `null` | Reserved for future enrichment |
| `schema_version` | `string` | always present | Contract marker for the JSON document shape |
| `schemaVersion` | `integer` | always present | Compatibility version used by the in-memory model |
| `author` | `string` | always present, may be `null` | Normalized author value |
| `source_author` | `string` | present when set, otherwise omitted | Original author value from the source |
| `created` | `string` | always present, may be `null` | Creation timestamp when available |
| `source_file` | `string` | present when set, otherwise omitted | Caller-supplied or preserved source file name |
| `source_engine` | `string` | always present | Always `TrueParserCad` |
| `metadata` | `object` | always present | Passthrough or empty object |
| `extents` | `object` | always present, may be `null` | Drawing-level extents |
| `layer_count` | `integer` | present when available | Hoisted summary value |
| `block_count` | `integer` | present when available | Hoisted summary value |
| `block_entity_count` | `integer` | present when available | Hoisted summary value |
| `entity_count` | `integer` | present when available | Hoisted summary value |
| `layers` | `array` | always present | List of layer records |
| `blocks` | `array` | always present | List of block records |
| `blockEntities` | `object` | always present | Dictionary keyed by block identifier |
| `entities` | `array` | always present | List of top-level entity records |

### Rules

- `schema_version` is the contract marker for the output JSON format.
- `schemaVersion` is retained for compatibility with the in-memory model and tests.
- `format` and `source_format` intentionally carry the same value for CAD so this contract stays aligned with other parser families that may distinguish them.
- `format_family` is always `cad`.
- `document_name` defaults to `source_file` when no better display name exists.
- `source_format`, `source_units`, `source_author`, and `source_file` are provenance fields.
- `author` is the normalized author value, while `source_author` preserves the raw source author when available.
- `source_engine` is always `TrueParserCad`.
- `metadata` is always present and uses `{}` when empty.
- `extents` reflects drawing-level extents, not per-entity bounds.
- `layer_count`, `block_count`, `block_entity_count`, and `entity_count` are summary values that are present when the internal terminal summary can provide them.
- `blockEntities` is keyed by block identifier and contains arrays of emitted entities for that block.
- `entities` contains only top-level emitted entities.
- `author`, `created`, and `extents` may serialize as explicit `null` values when the parser cannot derive a value.
- `layers`, `blocks`, `blockEntities`, and `entities` are always emitted because the in-memory model initializes them to empty collections.

### Document Field Mapping

This table is the authoritative mapping from the internal CAD document model to the public JSON document envelope.

| Internal Source | Public Placement | Public Field Name | Note |
| --- | --- | --- | --- |
| document `Format` / source format | `document` | `format` | Normalized public format value |
| constant | `document` | `format_family` | Always `cad` |
| source file name / display name derivation | `document` | `document_name` | Defaults to `source_file` |
| document `SourceFile` | `document` | `source_file` | Caller-supplied or preserved source file name |
| document `Metadata.SourceFormat` | `document` | `source_format` | Provenance field |
| document `Metadata.DocumentFormat` | `document` | `document_format` | Preserved when available |
| document `Metadata.Units` | `document` | `units` | Consumer-facing units |
| document `Metadata.SourceUnits` | `document` | `source_units` | Original source units |
| document `Metadata.Version` | `document` | `version` | Source document version |
| reserved / external enrichment | `document` | `title` | Commonly `null` |
| reserved / external enrichment | `document` | `subject` | Commonly `null` |
| reserved / external enrichment | `document` | `company` | Commonly `null` |
| document `SchemaContractVersion` | `document` | `schema_version` | Contract marker |
| document `SchemaVersion` | `document` | `schemaVersion` | Compatibility version |
| document `Metadata.Author` | `document` | `author` | Normalized author value |
| document `Metadata.SourceAuthor` | `document` | `source_author` | Original author value |
| document `Metadata.Created` | `document` | `created` | Creation timestamp when available |
| constant | `document` | `source_engine` | Always `TrueParserCad` |
| passthrough / empty object | `document` | `metadata` | Always present; `{}` when empty |
| summary `Extents` | `document` | `extents` | Hoisted from terminal summary |
| summary `LayerCount` | `document` | `layer_count` | Hoisted when available |
| summary `BlockCount` | `document` | `block_count` | Hoisted when available |
| summary `BlockEntityCount` | `document` | `block_entity_count` | Hoisted when available |
| summary `EntityCount` | `document` | `entity_count` | Hoisted when available |

### Mapping Rules

- The mapping table is the single source of truth for orchestrator implementation.
- Fields listed here must be mapped explicitly rather than inferred from neighboring fields.
- Reserved fields such as `title`, `subject`, and `company` should be emitted as `null` unless external enrichment populates them.
- `author` and `source_author` are intentionally distinct: `author` may be normalized or suppressed, while `source_author` preserves the raw source author value when available.
- `format` and `source_format` are intentionally distinct fields but carry the same CAD value today for cross-family consistency.
- `metadata` is always present, even when empty.
- `format_family` is always `cad`, and `source_engine` is always `TrueParserCad`.

---

## 5. Extents

`extents` is an object with the following shape:

```json
{
  "min": [0.0, 0.0],
  "max": [100.0, 50.0]
}
```

### Fields

| Field | Type | Notes |
| --- | --- | --- |
| `min` | `double[]` | `[minX, minY]` |
| `max` | `double[]` | `[maxX, maxY]` |

### Rules

- Extents are drawing-level bounds.
- They are derived from emitted entity bounds when available.
- If no bounds can be determined, `extents` may be `null` or omitted depending on the serializer path.

---

## 6. Layers

`layers` is an array of layer objects.

### Example

```json
{
  "name": "A-WALL",
  "color": 7,
  "linetype": "CONTINUOUS",
  "visible": true,
  "frozen": false,
  "locked": false
}
```

### Fields

| Field | Type | Emitted shape | Notes |
| --- | --- | --- | --- |
| `name` | `string` | present when set, otherwise omitted | Layer name preserved exactly from the source |
| `color` | `integer` | present when set, otherwise omitted | ACI color index when available |
| `linetype` | `string` | present when set, otherwise omitted | Layer linetype name |
| `visible` | `boolean` | always present | Visibility flag |
| `frozen` | `boolean` | always present | Frozen state |
| `locked` | `boolean` | always present | Locked state |

### Rules

- Layer names are preserved exactly as they appear in the source drawing.
- Layer records are additive and may carry more fields in future versions.

---

## 7. Blocks

`blocks` is an array of block definition objects.

### Example

```json
{
  "id": "25",
  "name": "*Model_Space",
  "entityIds": ["A1"],
  "isModelSpace": true,
  "isPaperSpace": false
}
```

### Fields

| Field | Type | Emitted shape | Notes |
| --- | --- | --- | --- |
| `id` | `string` | present when set, otherwise omitted | Deterministic block identifier |
| `name` | `string` | present when set, otherwise omitted | Block name preserved from the source |
| `entityIds` | `string[]` | always present | Entity identifiers that belong to the block |
| `isModelSpace` | `boolean` | always present | True when the block is model space |
| `isPaperSpace` | `boolean` | always present | True when the block is paper space |

### Rules

- Block records describe the block definition, not flattened INSERT instances.
- INSERT instances are represented as regular entities with geometry and transform metadata.
- `entityIds` preserves the block's internal entity membership.

---

## 8. Block Entities

`blockEntities` is a dictionary from block identifier to an array of emitted entity records.

### Example

```json
{
  "45265": [
    {
      "id": "45266",
      "type": "MTEXT",
      "coverageState": "supported",
      "containerId": "45265"
    }
  ]
}
```

### Rules

- The dictionary key identifies the parent block.
- Each value is an array of entity objects belonging to that block.
- Block entities use the same entity shape as top-level entities.

---

## 9. Entity Records

Entity records are the most important part of the output JSON contract.

### Shared Fields

Every emitted entity record may contain:

- `id`
- `type`
- `layer`
- `geometry`
- `bbox`
- `centroid`
- `wkt`
- `coverageState`
- `space`
- `containerId`
- `parentId`
- `children`
- `relations`
- `properties`

### Example

```json
{
  "id": "A1",
  "type": "LINE",
  "layer": "FRONT PANEL",
  "space": "model",
  "containerId": "25",
  "geometry": {
    "type": "Line",
    "coordinates": [
      [0.0, 0.0],
      [10.0, 0.0]
    ]
  }
}
```

### Fields

| Field | Type | Emitted shape | Notes |
| --- | --- | --- | --- |
| `id` | `string` | always present | Emitted entity identifier |
| `type` | `string` | always present | Base CAD entity name |
| `layer` | `string` | present when set, otherwise omitted | Source layer name |
| `geometry` | `object` | present when set, otherwise omitted | Normalized geometry payload |
| `bbox` | `double[]` | present when set, otherwise omitted | `[minX, minY, maxX, maxY]` |
| `centroid` | `double[]` | present when set, otherwise omitted | `[x, y]` |
| `wkt` | `string` | present when set, otherwise omitted | Spatial convenience representation |
| `coverageState` | `string` | always present | Completeness classification |
| `space` | `string` | present when set, otherwise omitted | Typically `model` or `paper` |
| `containerId` | `string` | present when set, otherwise omitted | Ownership container identifier |
| `parentId` | `string` | present when set, otherwise omitted | Emitted hierarchy parent |
| `children` | `string[]` | always present | Child entity identifiers |
| `relations` | `object` | always present | Provenance and hierarchy links |
| `properties` | `object` | always present | Traceability and source-specific extras |

### Rules

- `coverageState` is required on every emitted entity.
- `containerId` and `parentId` are not synonyms.
- `children` is derived from emitted parent-child relationships.
- `relations` is used for provenance and hierarchy linkage when relation resolution is enabled.
- `properties` preserves source-specific metadata, flattening results, and non-fatal extraction details.
- `children`, `relations`, and `properties` are initialized to empty collection values in the model, so they serialize even when no values are added.

---

## 10. Relations

`relations` is a flat string map used on `entity` and `block_entity` records.

### Common keys

- `source_handle`
- `owner_handle`
- `parent_id`

### Rules

- `source_handle` preserves the original source handle when available.
- `owner_handle` preserves the owning record handle.
- `parent_id` preserves emitted hierarchy parentage.
- Additional keys may be added in future versions, but these keys are the core contract.

### Block Source Mapping

Block records do not use relation fields in the internal parser output, so downstream projections must derive block provenance from the block's own handle-bearing field rather than from `relations`.

- `source_ref.source_handle` for a block should come from the block's source `Handle` or equivalent handle-bearing field
- `source_ref.owner_handle` may remain `null` unless the source explicitly provides ownership
- `source_ref.block_id` should remain `null` for the block record itself
- `source_ref.space` should remain `null` unless the block is explicitly tied to model or paper space metadata

For records materialized from block definitions, `source_ref.block_id` may reference the parent block identifier, but that is a downstream projection decision and should not be confused with emitted hierarchy parentage.

### Relation Projection Guidance

In the compact projection, `attributes.relations` is usually empty for ordinary entities because the core relation keys are already projected elsewhere:

- `source_handle` and `owner_handle` are normalized into `source_ref`
- `parent_id` is normalized into the base hierarchy fields

The primary reason to keep `attributes.relations` is additive relation data that does not fit those core fields, such as:

- `block_id` on block-attached or block-definition-derived records
- future additive relation keys that are not yet promoted into the base contract

If `attributes.relations` is empty, that is expected and valid.

---

## 11. Coverage State

`coverageState` is used to signal how completely a CAD entity is represented.

### Allowed values

- `supported`
- `metadata-only`
- `recognized-unsupported`

### Meaning

#### `supported`

The entity has structured normalized geometry support.

#### `metadata-only`

The entity is recognized, but geometry extraction is limited.

#### `recognized-unsupported`

The entity is recognized, but emitted through a fallback representation rather than a fully normalized geometry model.

### Expected mapping

#### `recognized-unsupported`

- `TABLE`
- `PROXYENTITY`
- `UNKNOWNENTITY`
- `OLEFRAME`

#### `metadata-only`

- `REGION`
- `BODY`
- `3DSOLID`
- `CADBODY`

#### `supported`

- all other currently normalized CAD entities
- entities that currently fall back to a supported record with `null` geometry when the parser does not yet have a typed normalizer path

---

## 12. Geometry Contract

`geometry` is a typed payload whose exact shape depends on the entity kind.

### Rules

- `geometry.type` is the primary routing key for downstream processors.
- Geometry payloads are entity-specific.
- New geometry fields may be added additively.
- `geometry` is a first-class output field and is not buried inside `properties`.
- Geometry field names are normalized to `snake_case` in the public compact contract, including nested geometry objects.
- Internal PascalCase geometry names such as `Start`, `End`, and `InsertPoint` become public fields such as `start`, `end`, and `insert_point`.
- Base entity names are preserved in `type`, while subtype detail lives in `geometry` and/or `properties`.
- For subtype-aware families, the parser must not collapse semantic variants without preserving subtype detail.

### Geometry types

- `Point`
- `Insert`
- `Line`
- `Polyline`
- `Circle`
- `Arc`
- `Text`
- `Ellipse`
- `Spline`
- `Hatch`
- `Dimension`
- `Leader`
- `MultiLeader`
- `Solid`
- `InfiniteLine`
- `Ray`
- `Face3D`
- `Mesh`
- `Image`
- `Viewport`
- `Wipeout`
- `Attribute`
- `Attrib`
- `AttDef`
- `Tolerance`
- `Shape`
- `Vertex`
- `MLine`
- `PdfUnderlay`
- `Ole2Frame`
- `Region`
- `Body`
- `Solid3D`
- `CadBody`
- `UnsupportedEntity`
- `Acis`

### Selected geometry notes

#### `Insert`

`Insert` geometry may include:

- `insertPoint`
- `rotation`
- `scale`
- `transformMatrix`
- `worldTransformMatrix`
- `blockName`
- `blockHandle`

#### `Line`

`Line` geometry uses coordinate pairs for the segment endpoints.

#### `Polyline`

`Polyline` geometry may include:

- `coordinates`
- `bulges`
- `isClosed`
- `constantWidth`

#### `Circle`

`Circle` geometry may include:

- `center`
- `radius`

#### `Arc`

`Arc` geometry may include:

- `center`
- `radius`
- `startAngle`
- `endAngle`

#### `Ellipse`

`Ellipse` geometry may include:

- `center`
- `majorAxis`
- `radiusX`
- `radiusY`
- `rotation`
- `radiusRatio`
- `startParameter`
- `endParameter`

`startParameter` and `endParameter` define the visible arc span for partial ellipses, so they are preserved explicitly rather than collapsed into a generic boundary flag.

#### `Text`

`Text` geometry may include:

- `insertPoint`
- `value`
- `height`

#### `Dimension`

`Dimension` geometry may include:

- `subtype`
- `measurement`
- `textOverride`
- `definitionPoint`
- `textMiddlePoint`
- `points`

#### `Leader`

`Leader` geometry may include:

- `vertices`
- `pathType`
- `arrowHeadEnabled`

#### `MultiLeader`

`MultiLeader` geometry may include:

- `leaderLines`
- `contentType`
- `attachedText`
- `blockName`
- `blockHandle`

#### `Hatch`

`Hatch` geometry may include:

- `patternName`
- `boundaryPaths`

#### `Vertex`

`Vertex` geometry may include:

- `location`
- `bulge`
- `startWidth`
- `endWidth`
- `vertexSubtype`

#### `UnsupportedEntity`

`UnsupportedEntity` geometry may include:

- `entityType`
- `reason`

#### `Acis`

`Acis` geometry may include:

- `entityType`

ACIS-related entities are generally represented with `metadata-only` coverage and may also carry limited traceability metadata and fallback spatial fields when available.

#### `Ole2Frame`

`Ole2Frame` geometry may include:

- `upperLeftCorner`
- `lowerRightCorner`
- `oleObjectType`
- `sourceApplication`
- `binaryData`
- `binaryDataLength`

`binaryData` is serialized as base64-encoded JSON text when present, following the default `System.Text.Json` byte-array encoding behavior. `binaryDataLength` is preserved as a lightweight size indicator for downstream consumers that do not need the full payload.

---

## 13. Spatial Metadata

Spatial metadata is emitted when the parser can derive it.

### Fields

- `bbox`
- `centroid`
- `wkt`

### Rules

- `bbox` is a 2D XY-space extent represented as `[minX, minY, maxX, maxY]`.
- `centroid` is a 2D XY-space point represented as `[x, y]`.
- `wkt` is a spatial convenience field for indexing and database ingestion.
- For non-planar or 3D entities, spatial values are projected to XY.
- Spatial output may be `null` when geometry support is incomplete or intentionally limited.
- Top-level entities may receive fallback bounds when first-class geometry does not provide spatial metadata.
- Block entities may remain `null` more often because they do not always receive the same fallback path.

### Currently spatially covered geometry

- `Point`
- `Line`
- `Polyline`
- `Circle`
- `Arc`
- `Text`
- `Ellipse`
- `Spline`
- `Hatch`
- `Ole2Frame`

### Currently spatially uncovered geometry

- `Insert`
- `Dimension`
- `Leader`
- `MultiLeader`
- `Solid`
- `Face3D`
- `Mesh`
- `Viewport`
- `Wipeout`
- `Image`
- `Ray`
- `InfiniteLine`
- `MLine`
- `PdfUnderlay`
- `Attribute`
- `Attrib`
- `AttDef`
- `Tolerance`
- `Shape`
- `Vertex`

### Hatch preservation

`Hatch` entities should preserve the distinction between solid fills and patterned hatches.

Minimum required semantics:

- `PatternName`
- `IsSolid`
- `BoundaryPaths`

Additional hatch metadata may be preserved additively.

---

## 14. Hierarchy And Ownership

### `containerId`

Ownership container.

Typical examples:

- model space container
- paper space container
- block record or block definition handle

### `parentId`

Emitted hierarchy parent.

Typical examples:

- flattened INSERT child relationships
- emitted instance trees

### Rules

- `containerId` is for ownership and reporting semantics.
- `parentId` is for emitted hierarchy semantics.
- They must not be treated as synonyms.
- `children` is derived from parent-child emitted relationships.

### Depth Note

This contract does not define a canonical `depth` field.

If a downstream projection derives `depth` for its own compact or UI-specific representation, it should be derived from emitted hierarchy only, not from CAD ownership:

- top-level records remain at `depth = 0`
- records nested via emitted `parentId` increase depth recursively
- block-definition membership alone should not change `depth`

Block-definition entities are owned by a block, but that containment is represented through ownership metadata such as `containerId` or block linkage, not through hierarchy. If an orchestrator chooses to represent block-definition members as children of a synthetic block parent, it must make that parentage rule explicit and keep it separate from the base CAD contract.

---

## 15. Fallback And Approximation Rules

### No silent drop

No recognized entity may be silently omitted only because full geometry support is unavailable.

### Catch-all fallback

If the parser encounters an entity type that is not explicitly classified as `recognized-unsupported` or `metadata-only`, it treats the entity as `supported` by coverage state and preserves traceability metadata.

If a typed geometry normalizer is not available yet, the emitted record may carry `geometry = null` while still preserving:

- entity type
- source identity
- ownership
- properties

### Unsupported recognized entities

Unsupported but recognized entities must still preserve:

- `type`
- `coverageState`
- `properties`
- spatial metadata when derivable
- fallback geometry marker where applicable

### Sequence markers

`SEQEND` is a structural marker for `POLYLINE` vertex lists and `INSERT` attribute lists.

It should not be materialized as a standalone canonical entity record because it carries no retrieval value of its own.

### ACIS entities

`REGION`, `BODY`, `3DSOLID`, and `CADBODY` depend on geometry not fully resolved by the parser.

Current behavior:

- first-class marker geometry or limited geometry metadata
- source bounds fallback for `bbox` when available
- `coverageState = "metadata-only"`
- traceability preserved in `properties`

### 3D and non-planar entities

For non-planar or 3D entities:

- `bbox` uses XY projection
- `centroid` uses XY projection
- `wkt` uses XY projection

### `RAY` and `XLINE`

`RAY` and `XLINE` may be represented through finite sampled segments for spatial output.

### Bulge handling

For `LWPOLYLINE` and bulged polyline segments:

- bulge metadata may be preserved
- sampled geometry may be used to improve `bbox` and `wkt`

---

## 16. Property Preservation

The `properties` object preserves source-specific and traceability metadata.

### Core traceability keys

The following keys are the core traceability fields that should survive into any downstream compact representation, whether they remain inside `properties` or are promoted into a downstream `attributes` object:

- `handle`
- `ownerHandle`
- `typeName`
- `instanceHandle`
- `blockName`
- `blockHandle`
- `sourceLayer`
- `xdata`
- `color`
- `lineweight`
- `linetype`
- `insertPoint`
- `rotation`
- `xScale`
- `yScale`
- `zScale`
- `transformMatrix`
- `worldTransformMatrix`
- `coverageState`
- `recognizedUnsupported`
- `unsupportedReason`
- `sourceObjectName`

`typeName` is the raw parser/ObjectName traceability value and is the source for any downstream `type_name` projection.

`transform_applied` is not a canonical field in this contract. If an orchestrator derives such a flag for its own enrichment pipeline, it must be documented separately and should not be treated as part of the base CAD output contract.

### Entity Color Mapping

For entity records, the source `Properties.color` value should be promoted into downstream `attributes.color` in any compact or search-oriented projection.

Expected shape:

```json
{
  "index": 256,
  "method": "bylayer",
  "rgb": null
}
```

Layer records may also carry color information, but this rule is specifically about entity-level color traceability.

### Text Projection Guidance

If a downstream compact or search-oriented projection derives a plain-text `text` field from this contract, use the following priority order:

1. `layer` or layer `name` for layer records.
2. `block` or block `name` for block records.
3. `TEXT`, `MTEXT`, `ATTRIB`, and `ATTDEF` use their direct text values when available.
4. `DIMENSION` prefers `geometry.text_override` when it is present and not `"<>"`.
5. `DIMENSION` otherwise prefers a formatted `geometry.measurement` value.
6. `MULTILEADER` prefers `geometry.attached_text` when present.
7. Records with both `tag` and `text_value` may use a combined form such as `"{tag}: {text_value}"`.
8. Records with a meaningful label-like geometry field may use that value.
9. Otherwise `text` should be `null`.

This guidance is intentionally explicit for downstream implementers, even though the current compact document does not expose a dedicated `text` field.

### Fallback Projection Examples

If a downstream compact projection materializes marker or fallback geometry, the geometry payload should remain structured and the spatial fields should reflect whatever the parser or orchestrator can derive.

#### Metadata-only example

```json
{
  "id": "ent-301",
  "type": "entity",
  "space": "model",
  "containerId": "*Model_Space",
  "geometry": {
    "type": "Solid3D"
  },
  "bbox": [0.0, 0.0, 120.0, 80.0],
  "centroid": [60.0, 40.0],
  "wkt": null
}
```

#### Recognized-unsupported example

```json
{
  "id": "ent-401",
  "type": "entity",
  "space": "model",
  "containerId": "*Model_Space",
  "geometry": {
    "type": "UnsupportedEntity",
    "entityType": "TABLE",
    "reason": "Entity recognized but not normalized in current parser."
  },
  "bbox": [0.0, 0.0, 250.0, 120.0],
  "centroid": [125.0, 60.0],
  "wkt": null
}
```

These examples show the intended behavior for downstream compact projections:

- marker geometry stays structured instead of collapsing to text
- source-bounds fallback may still populate `bbox` and `centroid` using the base array shape
- `wkt` may remain `null` when no reliable spatial projection is available

Common preserved values include:

- `handle`
- `ownerHandle`
- `typeName`
- `instanceHandle`
- `blockName`
- `blockHandle`
- `color`
- `lineweight`
- `linetype`
- `xdata`
- `sourceLayer`
- `insertPoint`
- `rotation`
- `xScale`
- `yScale`
- `zScale`
- `transformMatrix`
- `worldTransformMatrix`
- `bulges`
- `isClosed`
- `constantWidth`
- `dimensionSubtype`
- `definitionPoint`
- `textMiddlePoint`
- `measurement`
- `textOverride`
- `firstPoint`
- `secondPoint`
- `angleVertex`
- `centerPoint`
- `dimensionArcPoint`
- `featureLocation`
- `leaderEndpoint`
- `leaderVertexCount`
- `arrowHeadEnabled`
- `leaderPathType`
- `associatedAnnotationHandle`
- `contentType`
- `hasContextData`
- `attachedText`
- `leaderRootCount`
- `leaderLineCount`
- `blockContentHandle`
- `blockContentName`
- `firstCorner`
- `secondCorner`
- `thirdCorner`
- `fourthCorner`
- `thickness`
- `basePoint`
- `direction`
- `edgeVisibilityFlags`
- `vertexCount`
- `faceCount`
- `subdivisionLevel`
- `imagePath`
- `clipType`
- `clipMode`
- `clipVertexCount`
- `center`
- `width`
- `height`
- `targetPoint`
- `viewDirection`
- `twistAngle`
- `scaleFactor`
- `acisLimited`
- `acisEntityType`
- `attributeKind`
- `tag`
- `textValue`
- `insertionPoint`
- `text`
- `justification`
- `mlineFlags`
- `shapeName`
- `definition`
- `underlayFile`
- `oleFrame`
- `upperLeftCorner`
- `lowerRightCorner`
- `oleObjectType`
- `sourceApplication`
- `binaryDataLength`
- `recognizedUnsupported`
- `unsupportedReason`
- `sourceObjectName`
- `vertexSubtype`
- `polylineSubtype`
- `isClosed`
- `mVertexCount`
- `nVertexCount`
- `smoothSurface`
- `smoothType`

### Rules

- `properties` is additive.
- `properties` is the place for traceability and source-specific details.
- `properties` should preserve raw source values when possible.
- `coverageState` may be mirrored into `properties` in some extraction paths, but the top-level field remains authoritative.
- Non-fatal extraction notes should be promoted to the top-level `warnings` array when they describe document-level or record-level extraction issues.
- If a downstream projection also chooses to preserve a record-scoped note, it may do so in an additive `notes` or `note` field, but that is not required by the base contract.

---

## 17. NDJSON Stream Note

Although this file documents the final JSON document, the parser also supports NDJSON transport records.

NDJSON records use:

- `recordType`
- `value`
- `blockId`
- `entityCount`
- `extents`

Record sequence:

1. `document`
2. `layer`
3. `block`
4. `blockEntity`
5. `entity`
6. `summary`

### NDJSON rules

- one JSON object per line
- `recordType` identifies the record kind
- `document` contains document-level metadata in `value`
- `blockEntity` records include `blockId`
- `summary` is terminal

---

## 18. Invariants

The output JSON contract must preserve these invariants:

- exact source layer names, block names, and entity type names
- no silent loss of recognized entities
- explicit completeness signaling through `coverageState`
- hierarchy and ownership preserved separately
- spatial metadata derived consistently
- internal transport contract separated from final client-facing JSON
- additive evolution without breaking existing consumers

---

## 19. Minimal Full Example

```json
{
  "format": "DWG",
  "format_family": "cad",
  "document_name": "sample.dwg",
  "source_format": "DWG",
  "document_format": "DWG",
  "units": "Millimeters",
  "source_units": "Millimeters",
  "version": "AC1027",
  "title": null,
  "subject": null,
  "company": null,
  "schema_version": "1.0",
  "schemaVersion": 1,
  "author": "Engineering Team",
  "source_author": "ACadSharp",
  "created": "2026-04-11T08:15:00Z",
  "source_file": "sample.dwg",
  "source_engine": "TrueParserCad",
  "metadata": {},
  "extents": {
    "min": [0, 0],
    "max": [10, 20]
  },
  "layers": [
    {
      "name": "FRONT PANEL",
      "color": 7,
      "linetype": "CONTINUOUS",
      "visible": true,
      "frozen": false,
      "locked": false
    }
  ],
  "blocks": [
    {
      "id": "25",
      "name": "*Model_Space",
      "entityIds": ["A1"],
      "isModelSpace": true,
      "isPaperSpace": false
    }
  ],
  "blockEntities": {
    "45265": [
      {
        "id": "45266",
        "type": "MTEXT",
        "coverageState": "supported",
        "containerId": "45265",
        "children": [],
        "relations": {},
        "properties": {}
      }
    ]
  },
  "entities": [
    {
      "id": "A1",
      "type": "LINE",
      "layer": "FRONT PANEL",
      "space": "model",
      "containerId": "25",
      "coverageState": "supported",
      "geometry": {
        "type": "Line",
        "coordinates": [
          [0, 0],
          [10, 0]
        ]
      },
      "children": [],
      "relations": {},
      "properties": {}
    }
  ]
}
```

This example includes the fields declared as always present in Section 4.

---

## 20. Versioning

| Contract | Version | Field |
| --- | --- | --- |
| CAD Output JSON | `1.0` | `schema_version` |

### Version notes

- `1.0` is the current output contract marker for the materialized JSON document.
- The contract is additive by design.
- The JSON document shape is intended to remain stable for downstream viewers, indexing, search, and automation.
