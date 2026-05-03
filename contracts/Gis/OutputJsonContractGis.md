# TrueParser.Gis Compact Output JSON Contract

**Version**: 1.0  
**Scope**: GIS  
**Purpose**: compact, client-facing materialized JSON for downstream search, analytics, RAG, and gen AI pipelines  
"docs/contracts/Gis/InternalNdJsonContractGis.md"
**Package that shoul dbe used for implementing this contract**: TrueParser.Gis --version 0.2.2
---

## 1. Purpose

This document defines the compact JSON projection produced from the internal GIS NDJSON stream.

It is the downstream, materialized JSON contract for consumers that do not want to process line-delimited transport directly. It is designed to preserve the semantic content, ordering, and fidelity of the internal NDJSON contract while presenting it as a single JSON document.

This contract is not a second competing schema. It is a projection of the internal NDJSON contract into a compact tree shape.

The compact output is intended for:

* search
* analytics
* RAG pipelines
* downstream enrichment
* document materialization
* debug and replay workflows

The compact output must preserve the same source identity, CRS fidelity, geometry fidelity, feature order, and diagnostic context that appear in the internal NDJSON contract.

---

## 2. Relationship To Internal NDJSON

The internal NDJSON contract is the canonical transport form. The compact JSON contract is the canonical materialized form.

The compact JSON document must be derived from the internal NDJSON stream without losing meaning or introducing conflicting alternate shapes.

### Canonical mapping

* internal `header` record -> top-level document envelope
* internal `schema` record -> one entry in `layers[]`
* internal `style` record -> one entry in `layers[].styles[]`
* internal `feature` record -> one entry in `layers[].features[]`
* internal `layer_summary` record -> inline layer summary fields on the matching layer object
* internal `error` record -> one entry in top-level `diagnostics[]`
* internal `summary` record -> top-level document summary fields

### Structural rule

There is one authoritative compact tree:

* top-level document envelope
* `layers[]`
* `layers[].features[]`
* `diagnostics[]`

There is no parallel top-level `features` dictionary and no separate `layer_summaries` array. Those patterns create duplicate sources of truth and are not part of this contract.

---

## 3. Design Rules

* **Projection, not redesign**: the compact JSON must preserve the meaning of the internal NDJSON contract.
* **Single source of truth**: layer data lives in `layers[]`; feature data lives in `layers[].features[]`.
* **Style fidelity preserved**: when the source exposes explicit style or symbology, preserve it in `layers[].styles[]` and/or feature-level style metadata.
* **Preserve order**: source layer order, source feature order, and diagnostic order must remain stable.
* **No silent loss**: recognized layers and features must not disappear because the geometry is partial or the transport is compact.
* **CRS fidelity first**: preserve both strong CRS identity and raw/resolved CRS details when available.
* **Geometry fidelity first**: preserve the source coordinate values, geometry family, and axis order as exposed by the parser pipeline.
* **Properties are mandatory**: every feature must carry a `properties` object.
* **Raw properties preserved**: if the parser captured a raw property payload, it must be retained as `properties_raw`.
* **Additive evolution**: optional fields may be added, but existing meaning must not be contradicted.
* **Compact output is not lossy normalization**: it is a shaped materialization of the internal stream.

---

## 4. Document Envelope

The top-level JSON document is a single object.

```json
{
  "format": "GPKG",
  "format_family": "gis",
  "document_format": "gis",
  "schema_version": "1.0",
  "schemaVersion": 1,
  "source_engine": "TrueParser.Gis",
  "source_file": "roads.gpkg",
  "is_compressed": false,
  "feature_count_source": "unknown",
  "source_feature_count": null,
  "layer_count": 1,
  "feature_count": 1,
  "warning_count": 0,
  "error_count": 0,
  "skipped_feature_count": 0,
  "parse_timestamp": "2026-04-11T08:15:00Z",
  "parser_name": "TrueParser.Gis",
  "parser_version": "1.0.0",
  "primary_crs": {
    "authority": "EPSG",
    "code": "4326",
    "name": "WGS 84"
  },
  "extents": {
    "min_x": 76.95,
    "min_y": 11.02,
    "max_x": 76.96,
    "max_y": 11.03
  },
  "metadata": {},
  "layers": [],
  "diagnostics": []
}
```

### Envelope fields

* `format`: `string` - normalized source format or driver family such as `GPKG`, `GEOJSON`, `SHP`, or `FILEGDB`
* `format_family`: `string` - always `"gis"`
* `document_format`: `string` - always `"gis"`
* `schema_version`: `string` - compact document version string
* `schemaVersion`: `number | string` - optional compatibility alias
* `source_engine`: `string` - parser identity, typically `TrueParser.Gis`
* `source_file`: `string | null` - source file name or display name when available
* `is_compressed`: `boolean`
* `feature_count_source`: `string`
* `source_feature_count`: `integer | null`
* `layer_count`: `integer | null`
* `feature_count`: `integer | null`
* `warning_count`: `integer | null`
* `error_count`: `integer | null`
* `skipped_feature_count`: `integer | null`
* `parse_timestamp`: `string`
* `parser_name`: `string`
* `parser_version`: `string`
* `primary_crs`: `object | null`
* `extents`: `object | null`
* `metadata`: `object`
* `layers`: `array`
* `diagnostics`: `array`

### Envelope rules

* `metadata` must exist, even if empty.
* `layers` must preserve source layer order.
* `diagnostics` must preserve emission order.
* `document_format` is the compact output family identifier, not the source format.
* `format` is the source format or driver family and should remain distinct from `document_format`.
* `schema_version` and `schemaVersion` may both be present for compatibility, but they must describe the same contract version.

---

## 5. Primary CRS

The top-level `primary_crs` preserves the strongest available dataset CRS identity.

Expected fields:

* `authority`: `string | null`
* `code`: `string | null`
* `name`: `string | null`
* `wkt`: `string | null`
* `raw_wkt`: `string | null`
* `normalized_wkt`: `string | null`
* `epsg`: `string | null`
* `has_unknown_crs`: `boolean | null`
* `is_geographic`: `boolean | null`
* `is_projected`: `boolean | null`
* `axis_order`: `string | null`
* `proj_json`: `object | null`
* `source`: `string | null`

### CRS rules

* Preserve the strongest stable identity when available.
* Preserve raw and resolved CRS forms additively when possible.
* Do not collapse source-declared CRS and parser-resolved CRS into a single ambiguous value.

---

## 6. Layer Object

Each entry in `layers[]` represents one dataset layer.

```json
{
  "id": "layer-1",
  "name": "roads",
  "source_type": "GPKG",
  "geometry_type": "LineString",
  "feature_count": 1,
  "source_feature_count": 1,
  "has_mixed_geometry": false,
  "extent_source": "features",
  "crs": {
    "authority": "EPSG",
    "code": "4326",
    "name": "WGS 84"
  },
  "bbox": {
    "min_x": 76.95,
    "min_y": 11.02,
    "max_x": 76.96,
    "max_y": 11.03
  },
  "fields": [],
  "styles": [],
  "metadata": {},
  "features": []
}
```

### Layer fields

* `id`: `string` - stable layer identifier
* `name`: `string` - source layer name exactly as preserved
* `source_type`: `string | null` - parser-emitted source type or driver name
* `geometry_type`: `string | null`
* `feature_count`: `integer | null`
* `source_feature_count`: `integer | null`
* `skipped_feature_count`: `integer | null`
* `has_mixed_geometry`: `boolean | null`
* `extent_source`: `string | null`
* `crs`: `object | null`
* `bbox`: `object | null`
* `fields`: `array`
* `styles`: `array`
* `metadata`: `object`
* `features`: `array`

### Layer rules

* `layers[]` must preserve source layer order.
* `features[]` inside each layer must preserve source feature order.
* `name` must be the exact source layer name.
* `source_type` must preserve the parser-emitted driver or source type.
* `feature_count` should reflect emitted features, not merely source declarations, when they differ.
* `features[]` is required on every layer, even when empty.
* `styles[]` is optional but should preserve source style order when the source exposes explicit style or symbology metadata.
* Layer-scoped styles belong in `styles[]`; feature-scoped styles may also be mirrored on the feature object when needed for reconstruction.

---

## 7. Field Schema

Field schema is preserved on each layer in the `fields[]` array.

### Field entry

```json
{
  "name": "id",
  "type": "integer",
  "source_type": "integer",
  "nullable": true,
  "precision": null,
  "scale": null,
  "length": null,
  "domain": null,
  "encoding": null,
  "original_name": "ID",
  "alias": "identifier"
}
```

### Field rules

* `name` preserves the source field name exactly.
* `type` preserves the parser/source-facing field type when available.
* `source_type`, `nullable`, `precision`, `scale`, `length`, `domain`, `encoding`, `original_name`, and `alias` are additive metadata.
* Additional field metadata may be added later, provided it does not change existing meaning.

---

## 7.1. Style Object

Style data is preserved on the owning layer in `styles[]` and may also be referenced from features when style is feature-scoped or feature-specific.

```json
{
  "id": "style-16",
  "parent_id": "layer-1",
  "name": "STANDARD",
  "kind": "layer",
  "color": {
    "index": 7,
    "method": "aci"
  },
  "line_type": "solid",
  "line_weight": "0.25",
  "fill": {
    "enabled": true
  },
  "opacity": 1.0,
  "text_style": null,
  "symbol": null,
  "metadata": {}
}
```

### Style fields

* `id`: `string | null`
* `parent_id`: `string | null`
* `name`: `string | null`
* `kind`: `string | null` - style scope or presentation category such as `layer`, `feature`, `text`, or `symbol`
* `color`: `object | null`
* `line_type`: `string | null`
* `line_weight`: `string | null`
* `fill`: `object | null`
* `opacity`: `number | null`
* `text_style`: `object | null`
* `symbol`: `object | null`
* `metadata`: `object`

### Style rules

* `styles[]` preserves source style order when style metadata is present.
* Style records are optional but recommended whenever the source exposes explicit symbology, presentation, or rendering hints.
* Style data should remain source-owned data, not viewer-default styling.
* Feature-level style references may be used when the style is not purely layer-scoped.

---

## 8. Feature Object

Each entry in `features[]` represents one extracted GIS feature.

```json
{
  "id": "roads:101",
  "feature_id": "101",
  "layer_id": "layer-1",
  "layer": "roads",
  "geometry_type": "LineString",
  "coverage_state": "supported",
  "source_ref": {
    "layer_id": "layer-1",
    "source": "roads.gpkg",
    "crs": "EPSG:4326"
  },
  "resolved": {
    "layer_name": "roads",
    "geometry_type": "LineString",
    "crs_name": "WGS 84"
  },
  "geometry": {
    "type": "LineString",
    "coordinates": [
      [76.95, 11.02],
      [76.96, 11.03]
    ]
  },
  "geometry_raw": {
    "wkt": "LINESTRING (76.95 11.02, 76.96 11.03)",
    "wkb_base64": "AQIAAAACAAAA...",
    "ogr_geometry_type": "wkbLineString",
    "geometry_type": "LineString",
    "dimension": 2,
    "srid": 4326,
    "is_linear": true,
    "is_linearized": true
  },
  "bbox": {
    "min_x": 76.95,
    "min_y": 11.02,
    "max_x": 76.96,
    "max_y": 11.03
  },
  "centroid": {
    "x": 76.955,
    "y": 11.025
  },
  "style": null,
  "properties": {
    "id": 101,
    "name": "Main Road"
  },
  "properties_raw": {
    "id": 101,
    "name": "Main Road"
  },
  "fidelity": {
    "is_valid": true,
    "is_repaired": false,
    "is_linearized": true,
    "has_z": false,
    "has_m": false
  },
  "metadata": {}
}
```

### Feature fields

* `id`: `string` - emitted feature identifier
* `feature_id`: `string | number | null` - source/native feature identifier
* `layer_id`: `string` - parent layer identifier
* `layer`: `string | null` - convenience layer name
* `geometry_type`: `string | null`
* `coverage_state`: `string` - required
* `source_ref`: `object | null`
* `resolved`: `object | null`
* `geometry`: `object | null`
* `geometry_raw`: `object | null`
* `bbox`: `object | null`
* `centroid`: `object | null`
* `style`: `object | null`
* `properties`: `object` - required, never null
* `properties_raw`: `object | null`
* `fidelity`: `object | null`
* `metadata`: `object`

### Feature rules

* `coverage_state` is required on every feature.
* `properties` must always exist as an object.
* `source_ref.layer_id` and `layer_id` are authoritative links back to the parent layer.
* `resolved.layer_name` is convenience-only.
* `geometry` is the normalized geometry payload.
* `geometry_raw` preserves source-adjacent geometry metadata and encodings.
* `bbox` and `centroid` are derived spatial metadata in source coordinate space.
* `style`, when present, preserves feature-scoped presentation metadata or a feature-level style reference.
* `properties_raw`, when present, must be preserved alongside `properties`.
* `fidelity` preserves validation, repair, and dimensionality state.

---

## 9. Geometry Contract

`geometry` is the typed, normalized geometry payload.

### Geometry rules

* `geometry.type` is the primary routing key for downstream processors.
* Geometry shapes are geometry-specific.
* Geometry must not be silently flattened from multi-part or collection forms into simpler single-part forms.
* Coordinate values must preserve source numeric precision as emitted by the parser.
* Z and M dimensions may be preserved additively when available.
* Empty geometries may be represented as `geometry: null` or as an explicit empty geometry object, but the behavior must be deterministic for a given engine version.

---

## 10. Geometry Raw

`geometry_raw` preserves parser-adjacent geometry encodings.

Expected fields:

* `wkt`: `string | null`
* `wkb_base64`: `string | null`
* `ogr_geometry_type`: `string | null`
* `geometry_type`: `string | null`
* `dimension`: `integer | null`
* `srid`: `integer | null`
* `is_linear`: `boolean | null`
* `is_linearized`: `boolean | null`

### Raw geometry rules

* `wkb_base64`, when emitted, must be little-endian WKB encoded as base64.
* `wkt` is optional convenience output.
* `ogr_geometry_type` should preserve the GDAL/OGR-facing geometry type when available.
* `geometry_type` may be retained as a source-facing alias.
* `dimension` should preserve 2D, 3D, or measured semantics when known.

---

## 11. Fidelity Object

`fidelity` carries validation and transport fidelity details.

Expected fields:

* `is_valid`: `boolean | null`
* `is_repaired`: `boolean | null`
* `is_linearized`: `boolean | null`
* `has_z`: `boolean | null`
* `has_m`: `boolean | null`

### Fidelity rules

* `fidelity` is optional but recommended whenever validation state matters.
* `is_valid` captures the primary validation outcome.
* `is_repaired` indicates whether geometry repair was applied.
* `is_linearized` indicates whether curves or non-linear structures were linearized.
* `has_z` and `has_m` preserve dimensional semantics when available.

---

## 12. Diagnostics

Recoverable issues are collected in the top-level `diagnostics[]` array.

```json
{
  "id": "err-1",
  "code": "geometry-parse-failed",
  "message": "Feature geometry could not be normalized; properties were preserved.",
  "severity": "warning",
  "layer_id": "layer-1",
  "feature_id": "204",
  "source": "GdalVectorAdapter",
  "metadata": {}
}
```

### Diagnostic fields

* `id`: `string | null`
* `code`: `string | null`
* `message`: `string` - required
* `severity`: `string | null`
* `layer_id`: `string | null`
* `feature_id`: `string | number | null`
* `source`: `string | null`
* `metadata`: `object`

### Diagnostic rules

* Diagnostics are additive and non-terminal.
* Diagnostics must preserve emission order.
* Fatal extraction failures may still stop document materialization.

---

## 13. CoverageState

`coverage_state` is required on every feature.

### Allowed values

* `supported`
* `geometry-null`
* `recognized-unsupported`

### Meaning

* `supported`: the feature has structured normalized geometry support.
* `geometry-null`: the feature is recognized and emitted, but geometry is absent, empty, invalid, or intentionally omitted while properties and metadata are preserved.
* `recognized-unsupported`: the source record is recognized, but geometry or source semantics could only be represented through fallback transport rather than a normalized geometry payload.

---

## 14. Spatial Metadata

Spatial metadata is derived from the normalized or source geometry and must preserve source coordinate space.

### BBox

```json
{
  "min_x": 76.95,
  "min_y": 11.02,
  "max_x": 76.96,
  "max_y": 11.03
}
```

### Centroid

```json
{
  "x": 76.955,
  "y": 11.025
}
```

### Rules

* `bbox` is always in source coordinate space.
* `centroid` is always in source coordinate space.
* Axis order must follow the parser/source interpretation used for extracted geometry.
* `extents` uses the same shape as `bbox`.

---

## 15. Schema And Attribute Rules

* `fields[]` preserves source field names exactly.
* Field type strings should preserve the parser/source-facing type when possible.
* Field metadata may include width, precision, nullability, scale, length, domain, encoding, original name, and alias.
* `properties` is required on every feature.
* `properties` must always be an object and never `null`.
* Source attribute names should be preserved exactly unless a documented normalization rule exists.
* Attribute values should preserve original scalar types when possible.

---

## 16. Summary And Counts

The compact document carries summary information on the top-level envelope and on each layer where relevant.

### Top-level summary fields

* `layer_count`: `integer | null`
* `feature_count`: `integer | null`
* `warning_count`: `integer | null`
* `error_count`: `integer | null`
* `primary_crs`: `object | null`
* `extents`: `object | null`
* `metadata`: `object`

### Layer summary fields

* `feature_count`: `integer | null`
* `source_feature_count`: `integer | null`
* `has_mixed_geometry`: `boolean | null`
* `extent_source`: `string | null`

### Rules

* Top-level counts summarize the whole document.
* Layer counts summarize the layer only.
* The compact model must not require a separate `layer_summaries[]` collection.

---

## 17. Invariants

The compact GIS output must preserve these invariants:

* one JSON object
* top-level document envelope first
* `layers[]` preserves source layer order
* `layers[].features[]` preserves source feature order
* `diagnostics[]` preserves emission order
* layer identity preserved
* feature identity preserved
* CRS fidelity preserved
* source coordinate space preserved for `bbox`, `centroid`, and `extents`
* `properties` is always present on every feature
* `properties` is never `null`
* `properties_raw`, when present, is preserved alongside normalized properties
* WKB, when emitted, is little-endian base64
* no silent loss of recognized layers or features
* explicit completeness signaling through `coverage_state`
* compact projection remains aligned with the internal NDJSON contract

---

## 18. Null And Omission Rules

* Null fields may be omitted when not required.
* Required fields must always be present.
* `metadata` must exist on the top-level document and on nested objects that declare it as required.
* `layers[]` must exist, even if empty.
* `features[]` must exist on each layer, even if empty.
* `diagnostics[]` must exist, even if empty.
* Additional fields may be added additively when unavailable or not yet materialized.

---

## 19. Minimal Full Example

```json
{
  "format": "GPKG",
  "format_family": "gis",
  "document_format": "gis",
  "schema_version": "1.0",
  "schemaVersion": 1,
  "source_engine": "TrueParser.Gis",
  "source_file": "roads.gpkg",
  "is_compressed": false,
  "feature_count_source": "unknown",
  "source_feature_count": 1,
  "layer_count": 1,
  "feature_count": 1,
  "warning_count": 0,
  "error_count": 0,
  "skipped_feature_count": 0,
  "parse_timestamp": "2026-04-11T08:15:00Z",
  "parser_name": "TrueParser.Gis",
  "parser_version": "1.0.0",
  "primary_crs": {
    "authority": "EPSG",
    "code": "4326",
    "name": "WGS 84"
  },
  "extents": {
    "min_x": 76.95,
    "min_y": 11.02,
    "max_x": 76.96,
    "max_y": 11.03
  },
  "metadata": {},
  "layers": [
    {
      "id": "layer-1",
      "name": "roads",
      "source_type": "GPKG",
      "geometry_type": "LineString",
      "feature_count": 1,
      "source_feature_count": 1,
      "has_mixed_geometry": false,
      "extent_source": "features",
      "crs": {
        "authority": "EPSG",
        "code": "4326",
        "name": "WGS 84"
      },
      "bbox": {
        "min_x": 76.95,
        "min_y": 11.02,
        "max_x": 76.96,
        "max_y": 11.03
      },
      "fields": [
        {
          "name": "id",
          "type": "integer",
          "nullable": false
        }
      ],
      "metadata": {},
      "features": [
        {
          "id": "roads:101",
          "feature_id": "101",
          "layer_id": "layer-1",
          "layer": "roads",
          "geometry_type": "LineString",
          "coverage_state": "supported",
          "source_ref": {
            "layer_id": "layer-1",
            "source": "roads.gpkg",
            "crs": "EPSG:4326"
          },
          "resolved": {
            "layer_name": "roads",
            "geometry_type": "LineString",
            "crs_name": "WGS 84"
          },
          "geometry": {
            "type": "LineString",
            "coordinates": [
              [76.95, 11.02],
              [76.96, 11.03]
            ]
          },
          "geometry_raw": {
            "wkt": "LINESTRING (76.95 11.02, 76.96 11.03)",
            "wkb_base64": "AQIAAAACAAAA...",
            "ogr_geometry_type": "wkbLineString",
            "geometry_type": "LineString",
            "dimension": 2,
            "srid": 4326,
            "is_linear": true,
            "is_linearized": true
          },
          "bbox": {
            "min_x": 76.95,
            "min_y": 11.02,
            "max_x": 76.96,
            "max_y": 11.03
          },
          "centroid": {
            "x": 76.955,
            "y": 11.025
          },
          "properties": {
            "id": 101,
            "name": "Main Road"
          },
          "fidelity": {
            "is_valid": true,
            "is_repaired": false,
            "is_linearized": true,
            "has_z": false,
            "has_m": false
          },
          "metadata": {}
        }
      ]
    }
  ],
  "diagnostics": []
}
```

---

## 20. Orchestrator Notes

The orchestrator consuming this compact GIS JSON must:

* read the compact object as a single materialized document
* preserve `layers[]` order
* preserve `features[]` order within each layer
* preserve `diagnostics[]` order
* use top-level summary fields as the compact completion state
* retain additive fields during reconstruction and downstream indexing
* avoid introducing a second, contradictory schema alongside this one
* treat this document as the compact projection of the internal NDJSON stream

---

## 21. Versioning

| Contract | Version | Field |
| --- | --- | --- |
| GIS Compact Output JSON | `1.0` | `schema_version` |

### Version notes

* `1.0` defines the first compact output projection for GIS.
* This document must remain aligned with `InternalNdJsonContractGis.md`.
* The compact contract is a materialized tree projection, not a replacement for the internal NDJSON transport contract.
