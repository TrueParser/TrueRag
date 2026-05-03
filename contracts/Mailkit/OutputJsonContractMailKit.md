
# TrueParser.MailKit — Compact Structured Output JSON Contract Specification

**Version**: `1.0`  
**Scope**: TrueParser.MailKit engine family  
**Input formats**: `pst`, `ost`, `msg`, `eml`, `mbox`, `emlx`, `mht`, `mhtml`, `tnef`, `p7s`, `p7m`  
**Use cases**: Analytics · Search · RAG · Generative AI Pipelines
# package for implementing this is  TrueParser.MailKit --version 0.1.8
---

## 1. Purpose

This specification defines the **final client-facing JSON output contract** returned to consumers by **TrueParser.MailKit**.

The MailKit engine internally emits streamed NDJSON according to the **TrueParser.MailKit NDJSON Contract Specification**. The orchestration layer is responsible for transforming that internal NDJSON transport into the final compact JSON defined here. NDJSON is not part of the public consumer-facing contract. :contentReference[oaicite:2]{index=2}

This compact contract is optimized for downstream:

- search
- analytics
- RAG
- GenAI pipelines

It preserves structure, provenance, and fidelity while avoiding an excessively wide per-record schema.

---

## 2. Design Principles

- **Final JSON is the client contract**
- **Compact but high-fidelity**
- **Stable universal block shape**
- **Format-specific details live under `attributes`**
- **Hierarchy and provenance are preserved**
- **Warnings are always surfaced**
- **Output is deterministic**
- **No duplicate canonical data across base fields and attributes**
- **Mail and rich-item distinctions remain explicit**
- **NDJSON transport details do not leak into the public contract**
- **Attachment exposure is request-controlled**
- **Parser-native type names remain intact**

These principles follow the OpenDoc compact output style for public shape and the MailKit internal NDJSON contract for semantics and constraints. 

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

* `schema_version` — string, always `"1.0"`
* `document` — document-level metadata
* `warnings` — array of warning strings
* `content` — ordered array of extracted content blocks

### Rules

* `content` MUST preserve the original structural order represented by the internal NDJSON stream.
* `warnings` MUST always be included, even when empty.
* `document` MUST always be included.

---

## 4. Document

```json
{
  "source_file": "sample.eml",
  "document_name": "sample.eml",
  "format": "eml",
  "format_family": "mime_message",
  "document_id": null,
  "content_hash": null,
  "mime_type": null,
  "title": "Quarterly Report",
  "author": null,
  "subject": "Quarterly Report",
  "company": null,
  "created_at": null,
  "modified_at": null,
  "is_partial": false,
  "metadata": {},
  "record_count": 5,
  "folder_count": null,
  "message_count": 1,
  "item_count": 1,
  "options": {
    "include_attachments": false,
    "include_body_rtf": false
  }
}
```

### Fields

* `source_file`: `string | null`
* `document_name`: `string | null`
* `format`: `string`
* `format_family`: `"mail_archive" | "mail_message" | "mime_message" | "mail_container" | "security_wrapper"`
* `document_id`: `string | null`
* `content_hash`: `string | null`
* `mime_type`: `string | null`
* `title`: `string | null`
* `author`: `string | null`
* `subject`: `string | null`
* `company`: `string | null`
* `created_at`: `string | null`
* `modified_at`: `string | null`
* `is_partial`: `boolean`
* `metadata`: `object`
* `record_count`: `integer | null`
* `folder_count`: `integer | null`
* `message_count`: `integer | null`
* `item_count`: `integer | null`
* `options`: `object`

### `format_family` Mapping

The orchestrator derives `format_family` from `format` using this mapping:

* `pst`, `ost` -> `mail_archive`
* `msg` -> `mail_message`
* `eml`, `emlx`, `mht`, `mhtml` -> `mime_message`
* `mbox` -> `mail_container`
* `tnef`, `p7s`, `p7m` -> `security_wrapper`

### Rules

* `format` MUST preserve the concrete source subtype from the parser contract. 
* `document_name` MUST preserve the parser-assigned document name when available.
* `document_id`, when present, SHOULD carry the deterministic source identifier used for deduplication and correlation.
* `content_hash`, when present, SHOULD carry the deterministic document fingerprint used for deduplication and integrity checks.
* `mime_type`, when present, SHOULD carry the source MIME type.
* `author` and `company` are cross-engine envelope fields inherited from the compact public schema; for MailKit they are normally `null` and MUST NOT be inferred from sender, recipient, or contact data.
* `subject` MAY be hoisted from a top-level `message` or rich item when the source has a single primary root item.
* `title` MAY be hoisted from source metadata or fall back to a primary subject.
* `created_at` and `modified_at` may be `null` if unavailable.
* `is_partial` MUST always be present.
* `metadata` MUST always be present.
* `options` SHOULD reflect effective parse options that shape public output, especially `IncludeAttachments` and `IncludeBodyRtf` from the internal NDJSON contract.

---

## 5. Warnings

```json
[
  "Attachment parsing was disabled by options.",
  "Some MIME headers were malformed and recovered best-effort."
]
```

### Fields

* `warnings`: `string[]`

### Rules

* Warnings MUST always be included, even when empty.
* Warnings are plain strings in the public contract.
* The orchestrator MUST preserve warning text without inventing structured codes unless a future parser version emits them.
* Warnings are disclosures only. They do not legitimize parser-side splitting, truncation, summarization, or fidelity loss. 

---

## 6. Universal Base Block Contract

Every meaningful extracted unit in `content` MUST use this base shape.

```json
{
  "id": "msg_0001",
  "type": "message",
  "order": 1,
  "path": ["Inbox"],
  "parent_id": "fld_0001",
  "depth": 1,
  "page_number": null,
  "source_ref": {
    "folder_path": "Inbox",
    "folder_id": null,
    "message_id": "<abc@example.com>",
    "mime_part_index": null,
    "content_id": null,
    "item_class": "IPM.Note",
    "uid": null,
    "record_index": null,
    "name": null
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "Quarterly Report",
  "attributes": {}
}
```

### Fields

* `id`: `string`
* `type`: `string`
* `order`: `integer`
* `path`: `string[] | null`
* `parent_id`: `string | null`
* `depth`: `integer`
* `page_number`: `integer | null`
* `source_ref`: `SourceRef | null`
* `is_inferred`: `boolean`
* `warnings`: `string[]`
* `content_hash`: `string | null`
* `text`: `string | null`
* `attributes`: `object`

### Rules

* `id` MUST be deterministic.
* `type` MUST preserve the parser-emitted semantic type.
* `order` MUST preserve the parser-emitted traversal order and MUST match the block's position in the source order.
* `path` is the canonical public structural path, usually folder or container lineage.
* `parent_id` is `null` only for root blocks.
* `depth` MUST never be `null`.
* `is_inferred` MUST never be `null` and MUST be `false` for MailKit output, because the MailKit parser does not invent inferred structure.
* `warnings` MUST always be present on content blocks, even when empty.
* `content_hash`, when present, MAY carry a deterministic per-block fingerprint for deduplication or integrity checks.
* `page_number` remains a first-class base field for cross-engine consistency, but will normally be `null` for MailKit.
* `attributes` contains only type-specific fields not promoted into the universal base.

This base shape follows the OpenDoc compact public style, while the content semantics come from the MailKit internal NDJSON contract.

---

## 7. SourceRef

```json
{
  "folder_path": "Inbox",
  "folder_id": null,
  "message_id": "<abc@example.com>",
  "mime_part_index": 1,
  "content_id": null,
  "item_class": "IPM.Note",
  "uid": null,
  "record_index": null,
  "name": null
}
```

### Fields

* `folder_path`: `string | null`
* `folder_id`: `string | null`
* `message_id`: `string | null`
* `mime_part_index`: `integer | null`
* `content_id`: `string | null`
* `item_class`: `string | null`
* `uid`: `string | null`
* `record_index`: `integer | null`
* `name`: `string | null`

### Rules

* `source_ref` is the canonical normalized provenance object.
* The orchestrator MUST NOT invent trace anchors the parser did not emit.
* `record_index`, when present, is the zero-based typed-record index in the internal NDJSON stream after the `document` header and corresponds to `order - 1` for typed records.
* Fields already normalized in `source_ref` MUST NOT be duplicated inside `attributes`.
* The orchestrator may either:

  * emit only populated fields, or
  * normalize all fields with explicit nulls

Pick one and keep it consistent.

### Provenance Mapping from NDJSON

This public shape is derived from the internal NDJSON `SourceRef`, `Path`, `SourceIdentity`, and related source-local anchors, including folder path/id, message id, MIME part position, content id, item class, and UID-like rich-item identifiers where available. 

---

## 8. Block Type Naming

Block types MUST preserve the original semantic names emitted by the MailKit parser.

### Current parser-emitted type families

Examples include:

* `folder`
* `mailbox`
* `message`
* `embedded_message`
* `attachment`
* `body_part`
* `recipient`
* `appointment`
* `contact`
* `task`
* `distribution_list`
* `participant`
* `tnef_payload`
* `smime_entity`
* `signed_entity`
* `encrypted_entity` 

### Rules

* Do not rename block types unless required by a breaking public-contract revision.
* Do not collapse semantically distinct types.
* Keep mail items and rich items distinct.

---

## 9. Deterministic `text` Mapping Rule

The public contract includes a universal `text` field for downstream search, RAG, and GenAI consumption.

The orchestrator MUST populate `text` using this deterministic priority order:

1. `Text`
2. `Subject`
3. `DisplayName`
4. `Address`
5. if block has `Members`, `Emails`, `Phones`, or equivalent string-list content, join with newlines
6. `Location`
7. `FileName`
8. `Name`
9. `null`

### Rules

* `text` is the canonical downstream plain-text projection.
* Empty or whitespace-only results at any priority step are treated as unmatched; the orchestrator MUST proceed to the next step.
* When `text` is derived from a non-`Text` property, the original structured field may remain in `attributes` only if it adds semantic value beyond the plain-text projection.
* The parser MUST NOT emit Markdown or other synthetic formatting syntax in text fields, and the public contract inherits that rule. 

---

## 10. Attributes

`attributes` is a format-specific object containing additional structured fields for the block.

### Rules

* `attributes` should always be present.
* Use `{}` when no additional type-specific fields exist.
* `attributes` MUST contain only fields relevant to the current block type.
* `attributes` MUST preserve original structured values where meaningful.
* `attributes` MUST NOT repeat normalized provenance fields already present in `source_ref`.
* Source-specific overflow properties from the parser's `SourceProperties` bag MUST be mapped into `attributes`.
* Derived attachment availability and source-kind hints such as `content_available` and `source_kind` are treated as source-specific overflow properties when emitted.
* `attributes` MAY repeat the canonical `text` value when the structured field is useful for search, analytics, or fidelity.

### Canonical examples

* Keep header maps and internet-header details on `message`.
* Keep recipient role and address metadata on `recipient`.
* Keep body encoding and media-type details on `body_part`.
* Keep attachment file metadata, inline flags, content availability, and source-kind details on `attachment`.
* Keep recurrence metadata on `appointment`.
* Keep structured phone/email/address collections on `contact`.
* Keep completion/status metadata on `task`.
* Keep membership collections on `distribution_list`.
* Keep preservation metadata on `tnef_payload`, `smime_entity`, `signed_entity`, and `encrypted_entity`.

---

## 11. Content Model

`content` is always an ordered array of blocks using the universal base contract.

Format-specific details must go inside `attributes`.

The array position preserves source order, and each block's `order` field provides the explicit source-order ordinal so consumers can detect filtered gaps or correlate back to the internal NDJSON stream.

### Supported type groups

#### Mail / Container Types

Examples:

* `mailbox`
* `folder`
* `message`
* `embedded_message`
* `recipient`
* `body_part`
* `attachment`

#### Rich Item Types

Examples:

* `appointment`
* `contact`
* `task`
* `distribution_list`
* `participant`

#### Preservation / Wrapper Types

Examples:

* `tnef_payload`
* `smime_entity`
* `signed_entity`
* `encrypted_entity`

These groups and type names come directly from the internal MailKit NDJSON contract. 

---

## 12. Fixed Compact Examples

### Message

```json
{
  "id": "msg_0001",
  "type": "message",
  "order": 1,
  "path": ["Inbox"],
  "parent_id": "fld_0001",
  "depth": 1,
  "page_number": null,
  "source_ref": {
    "folder_path": "Inbox",
    "message_id": "<abc@example.com>",
    "item_class": "IPM.Note"
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "Quarterly Report",
  "attributes": {
    "subject": "Quarterly Report",
    "sent_at": "2026-04-11T09:00:00Z",
    "received_at": "2026-04-11T09:01:00Z",
    "importance": "normal",
    "sensitivity": "normal",
    "is_signed": false,
    "is_encrypted": false,
    "conversation_topic": "Quarterly Report"
  }
}
```

### Recipient

```json
{
  "id": "rcp_0001",
  "type": "recipient",
  "order": 2,
  "path": ["Inbox"],
  "parent_id": "msg_0001",
  "depth": 2,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "jane@example.com",
  "attributes": {
    "role": "from",
    "display_name": "Jane Smith",
    "address": "jane@example.com"
  }
}
```

### Body part

```json
{
  "id": "body_0001",
  "type": "body_part",
  "order": 3,
  "path": ["Inbox"],
  "parent_id": "msg_0001",
  "depth": 2,
  "page_number": null,
  "source_ref": {
    "mime_part_index": 1,
    "content_id": null
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "Hello world",
  "attributes": {
    "body_kind": "plain_text",
    "media_type": "text/plain",
    "charset": "utf-8",
    "content_transfer_encoding": "quoted-printable"
  }
}
```

### Attachment

```json
{
  "id": "att_0001",
  "type": "attachment",
  "order": 4,
  "path": ["Inbox"],
  "parent_id": "msg_0001",
  "depth": 2,
  "page_number": null,
  "source_ref": {
    "content_id": "img1@example.com"
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "invoice.pdf",
  "attributes": {
    "file_name": "invoice.pdf",
    "media_type": "application/pdf",
    "size": 182340,
    "is_inline": false,
    "content_available": true,
    "source_kind": "EmbeddedStream"
  }
}
```

### Appointment

```json
{
  "id": "apt_0001",
  "type": "appointment",
  "order": 1,
  "path": null,
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": {
    "uid": "040000008200E00074C5B7101A82E00800000000"
  },
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "Weekly Sync",
  "attributes": {
    "subject": "Weekly Sync",
    "start": "2026-04-11T09:00:00Z",
    "end": "2026-04-11T09:30:00Z",
    "location": "Room A",
    "meeting_status": 1,
    "is_all_day": false,
    "busy_status": "busy",
    "response_status": "accepted",
    "reminder_set": true,
    "reminder_minutes_before": 15
  }
}
```

### Contact

```json
{
  "id": "cnt_0001",
  "type": "contact",
  "order": 1,
  "path": null,
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "Jane Smith",
  "attributes": {
    "display_name": "Jane Smith",
    "given_name": "Jane",
    "surname": "Smith",
    "company": "Acme Corp",
    "emails": ["jane@example.com"],
    "phones": ["+1-555-0100"]
  }
}
```

### Task

```json
{
  "id": "tsk_0001",
  "type": "task",
  "order": 1,
  "path": null,
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "Prepare board deck",
  "attributes": {
    "subject": "Prepare board deck",
    "task_status": "in_progress",
    "percent_complete": 40,
    "due_date": "2026-04-15T00:00:00Z",
    "is_complete": false
  }
}
```

### Distribution list

```json
{
  "id": "dl_0001",
  "type": "distribution_list",
  "order": 1,
  "path": null,
  "parent_id": null,
  "depth": 0,
  "page_number": null,
  "source_ref": null,
  "is_inferred": false,
  "warnings": [],
  "content_hash": null,
  "text": "Exec Team",
  "attributes": {
    "display_name": "Exec Team",
    "members": [
      "ceo@example.com",
      "cfo@example.com",
      "cto@example.com"
    ]
  }
}
```

---

## 13. Attachment Behavior in Public Output

### Request behavior

* `includeAttachments` is fixed to `false` in the MailKit worker integration
* there is no client-facing toggle to enable attachments in this worker path
* attachment records MUST be omitted from `content`

### Rules

* This public behavior follows the attachment-gating semantics in the internal MailKit contract. 
* When attachments are omitted, the orchestrator MUST NOT preserve attachment presence markers such as `has_attachments`.
* Binary attachment payloads MUST NOT be inlined in the compact JSON contract.
* Attachment metadata and attachment-related source fields must not appear as standalone content blocks when attachment inclusion is disabled.

---

## 14. Invariants

The final JSON output must preserve these invariants:

* **Linear order preserved** — output ordering must follow source reading or structural order
* **Deterministic output** — same source input must produce identical output
* **Truthful extraction** — no summarization, paraphrasing, rewriting, or semantic invention
* **Structural fidelity** — hierarchy must remain explicit via `path`, `parent_id`, and `depth`
* **Stable identifiers** — IDs must be deterministic and stable
* **Source traceability** — each meaningful block must be traceable via `source_ref` when available
* **Inference transparency** — MailKit does not infer structure, so `is_inferred` is always `false` for MailKit output
* **Warning visibility** — warnings must be included in final output
* **No synthetic filler** — output must not invent empty grouping blocks or meaningless wrappers
* **Mail / rich-item fidelity** — rich items must not be collapsed into plain mail-only blocks when distinct structure is recoverable
* **No parser-side split / truncate leakage** — public output must not normalize or disguise parser-side fragmentation because the parser contract forbids it

These invariants come from the MailKit internal NDJSON contract, with the public-envelope style borrowed from the OpenDoc compact reference.

---

## 15. Null and Omission Rules

### Required fields

Required fields must always be present:

* `schema_version`
* `document`
* `warnings`
* `content`
* block base fields

### Optional fields

Optional fields may be omitted when not applicable.

### Document metadata

Document metadata fields should always be present in `document`, even when `null`.

### SourceRef

Choose one strategy and keep it consistent:

* emit only populated fields, or
* normalize all fields to explicit nulls

### Attributes

* `attributes` should always be present
* use `{}` when no additional type-specific fields exist

These omission/null rules follow the compact public-envelope pattern from the OpenDoc reference implementation. 

---

## 16. Minimal Full Example

```json
{
  "schema_version": "1.0",
  "document": {
    "source_file": "sample.eml",
    "document_name": "sample.eml",
    "format": "eml",
    "format_family": "mime_message",
    "document_id": null,
    "content_hash": null,
    "mime_type": null,
    "title": "Hello",
    "author": null,
    "subject": "Hello",
    "company": null,
    "created_at": null,
    "modified_at": null,
    "is_partial": false,
    "metadata": {},
    "record_count": 4,
    "folder_count": null,
    "message_count": 1,
    "item_count": 1,
    "options": {
      "include_attachments": false,
      "include_body_rtf": false
    }
  },
  "warnings": [],
  "content": [
    {
      "id": "msg_0001",
      "type": "message",
      "order": 1,
      "path": null,
      "parent_id": null,
      "depth": 0,
      "page_number": null,
      "source_ref": {
        "message_id": "<abc@example.com>"
      },
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "Hello",
      "attributes": {
        "subject": "Hello"
      }
    },
    {
      "id": "rcp_0001",
      "type": "recipient",
      "order": 2,
      "path": null,
      "parent_id": "msg_0001",
      "depth": 1,
      "page_number": null,
      "source_ref": null,
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "jane@example.com",
      "attributes": {
        "role": "from",
        "display_name": "Jane",
        "address": "jane@example.com"
      }
    },
    {
      "id": "rcp_0002",
      "type": "recipient",
      "order": 3,
      "path": null,
      "parent_id": "msg_0001",
      "depth": 1,
      "page_number": null,
      "source_ref": null,
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "john@example.com",
      "attributes": {
        "role": "to",
        "display_name": "John",
        "address": "john@example.com"
      }
    },
    {
      "id": "body_0001",
      "type": "body_part",
      "order": 4,
      "path": null,
      "parent_id": "msg_0001",
      "depth": 1,
      "page_number": null,
      "source_ref": {
        "mime_part_index": 1
      },
      "is_inferred": false,
      "warnings": [],
      "content_hash": null,
      "text": "Hello world",
      "attributes": {
        "body_kind": "plain_text",
        "media_type": "text/plain"
      }
    }
  ]
}
```

---

## 17. Implementation Notes for the Orchestrator

The orchestrator must:

* derive `format_family` from `format`
* preserve stable IDs, hierarchy, warnings, and inference markers
* keep `page_number` present and normally `null` for MailKit
* preserve or normalize `source_ref`
* derive `depth` from the parent chain and source traversal when the parser does not emit it, with root blocks using `0`
* hoist `title`, `subject`, and related metadata from top-level message or rich-item records where appropriate
* compute `record_count`, `folder_count`, `message_count`, and `item_count` when possible
* default unsupported metadata such as `created_at` and `modified_at` to `null`
* preserve original block type names without renaming
* map type-specific fields into `attributes` without dropping meaningful semantics
* treat attachment `content_available` and `source_kind` as source-specific overflow fields when the parser or orchestrator emits them
* apply the deterministic `text` mapping rule consistently
* ensure `depth` and `is_inferred` are never `null`
* keep attachment projection disabled in the worker integration
* preserve explicit wrapper/security entities when emitted by the parser

These responsibilities come from the MailKit internal NDJSON contract, with the public client shape patterned after the OpenDoc reference implementation.

---

## 18. Versioning

| Version | Change                                                                        |
| ------- | ----------------------------------------------------------------------------- |
| `1.0`   | Initial compact public structured output JSON contract for TrueParser.MailKit |

The key boundary here is simple: **MailKit internal NDJSON defines the truth; OpenDoc compact JSON only defines the public presentation pattern.**
