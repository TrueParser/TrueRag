# ADR 022: Hallucination Governance and Grounded Generation

## Status
Proposed

## Context
Hallucination risk is a top reliability issue for enterprise RAG systems.  
Even with strong retrieval quality, generated responses can still include:

- unsupported claims not present in retrieved evidence
- fabricated or invalid citations
- overconfident answers under weak/conflicting evidence
- inconsistent response structure that is difficult to validate

TrueRAG requires a governance architecture that treats grounded generation as a hard control plane, not best-effort prompt behavior.

## Decision
TrueRAG will implement a layered hallucination-governance architecture with mandatory controls for grounded-answer routes.

### 1. Hard Grounding Policy (Prompt Layer)
- Every grounded-generation LLM call includes a non-optional system policy:
  - answer only from retrieved evidence
  - do not fabricate claims or citations
  - explicitly abstain when evidence is insufficient/conflicting
- Policy injection is deterministic in prompt assembly.

### 2. Structured Output Contract (Schema Layer)
- Grounded outputs must follow a machine-validated schema (for example):
  - `answer`
  - `claims[]`
  - `citations[]`
  - `insufficiency_reason`
  - `confidence`
- Schema parsing/validation is mandatory before response finalization.
- Invalid schema output triggers bounded retry or abstain path.

### 3. Claim-to-Citation Validator (Verification Layer)
- Each factual claim must map to at least one valid retrieved evidence node.
- Citation IDs must resolve to in-scope, ACL-valid retrieved context.
- Uncited or invalidly cited claims cause reject/regenerate/abstain behavior.

### 4. Abstention and Refusal Thresholds (Policy Gate)
- Configurable thresholds use retrieval confidence, evidence coverage, and contradiction signals.
- If thresholds are not met, response must abstain/refuse deterministically.
- Optional partial-answer mode can return only supported subset + explicit unknowns.

### 5. Optional Verifier/Critic Pass (Second-Pass Governance)
- A bounded post-generation verifier may audit claim-evidence alignment.
- Outcomes:
  - `pass`
  - `revise`
  - `reject`
- Retry budget and latency limits are enforced.

### 6. Normalized End-User Response (Presentation Layer)
- Schema-rich model output is normalized into stable API responses.
- User-facing shape remains consistent while preserving evidence traceability.
- Internal governance metadata may be retained for audits/observability.

### 7. Evaluation and CI Quality Gates (Release Governance)
- Hallucination-focused eval suite is mandatory in CI:
  - unsupported claim detection
  - citation validity
  - abstention correctness
  - contradiction handling
- Quality thresholds gate merges/releases for grounded-answer paths.

## Enterprise Architecture Notes

### Boundaries and Ownership
- `TrueRag.Conversations`: policy prompt assembly, LLM orchestration, verifier loop.
- `TrueRag.Retrieval`: evidence identity, scope/ACL-safe citation resolution.
- `TrueRag.Core`: schema contracts and governance primitives.
- `TrueRag.Api`: normalized response mapping and route policy modes.
- `TrueRag.Host`: configuration ownership for thresholds/verifier flags/quality modes.

### Security and Compliance
- No raw provider secrets or sensitive prompt text in logs.
- Governance checks must preserve `tenant_id + app_id + collection_id` isolation.
- Citation validation must enforce ACL boundaries before response emission.

### Operational Characteristics
- Governance controls are deterministic and testable.
- Failure modes are explicit (`schema_invalid`, `citation_invalid`, `insufficient_evidence`, `verifier_reject`).
- Latency overhead is controlled with bounded retry/verifier loops.

## Consequences

Positive:
- Significantly reduces unsupported/fabricated outputs in grounded routes.
- Provides machine-checkable, auditable response correctness controls.
- Improves enterprise trust, compliance posture, and release safety.

Trade-offs:
- Additional latency and orchestration complexity.
- Higher implementation/test burden across conversations/retrieval/API modules.
- Requires active eval maintenance and CI governance tuning.

## Non-Goals
- Replacing retrieval quality improvements.
- Solving every conversational creativity use case with strict grounding.
- Removing human review need in high-stakes domains.

## Rollout Strategy
1. Enforce hard policy prompt + schema output on grounded routes.
2. Add claim-citation validator and abstention thresholds.
3. Add optional verifier pass with strict budgets.
4. Add hallucination eval suite and CI quality gates.
5. Expand to all production grounded-answer routes.
