# TrueRAG Conversations

The Conversations module provides Phase 3.1 conversation memory capabilities:

- thread persistence and retrieval
- conversation summary refresh
- active document/section scope tracking
- stateless API-node behavior via distributed ephemeral state
- deterministic prompt assembly and token budget policy
- swappable LLM provider abstraction with streaming/tool-call response contracts

## Responsibilities

1. Persist conversation messages by thread with tenant/app isolation.
2. Persist thread-level state (summary, active scope, refresh timestamp, turn count).
3. Keep active thread scope in distributed cache to support stateless API nodes.
4. Expose conversation endpoints through `TrueRag.Host`.

## API Surface (Current)

- `POST /api/v1/conversations/threads/{threadId}/turns`
  - Adds a user turn to a thread.
  - Refreshes thread summary and thread state.

- `GET /api/v1/conversations/threads/{threadId}?take={n}`
  - Returns a thread snapshot with messages and thread state.

- `POST /api/v1/conversations/threads/{threadId}/refresh?recentWindow={n}`
  - Rebuilds summary and thread state from recent messages.

- `POST /api/v1/rag/generate`
  - Adds user turn, assembles deterministic prompt, invokes selected LLM provider, persists assistant turn.
  - Request supports provider override and explicit prompt token budget.

## Storage Model

Durable state is persisted in database tables:

- `conversation_messages`
  - `thread_id`, `tenant_id`, `app_id`, `role`, `message`, `occurred_at_utc`
  - optional: `active_document_id`, `active_section_path`

- `conversation_thread_states`
  - `thread_id`, `tenant_id`, `app_id`
  - `summary`, `active_document_id`, `active_section_path`
  - `last_refreshed_at_utc`, `total_turns`

All queries are tenant/app scoped, aligned with multi-tenancy rules.

## Stateless Node Behavior

Ephemeral active thread scope is stored in `IDistributedCache` with scoped keys:

`conversation:state:{tenantId}:{appId}:{threadId}`

Host runtime can use:

- Redis-backed distributed cache (production)
- in-memory distributed cache (local/dev fallback)

## ADR Alignment

- ADR 002: Conversation memory and summary lifecycle
- ADR 008: Stateless API nodes with external ephemeral state
- ADR 012: Tenant/app isolation at data boundary

## Phase 3.2 Prompt and LLM Policy

1. Prompt assembly order is deterministic:
   - system instruction
   - thread summary
   - current user turn
   - retrieved context (by descending score)
   - recent conversation history

2. Token budget policy is explicit and deterministic:
   - prompt budget = total budget minus reserved completion tokens
   - required segments are kept first
   - optional segments are dropped by priority when budget is exceeded
   - truncation is deterministic (never random cut)

3. LLM providers are swappable through `ILlmProvider` + `ILlmProviderFactory`:
   - `local`
   - `openai`
   - `anthropic`

4. Streaming and tool-call contracts are provider-agnostic through core LLM models.

## Phase 10 Grounded Generation Governance

Grounded generation on `/api/v1/rag/generate` now enforces layered controls:

1. Pre-generation answerability gate:
   - blocks LLM execution when evidence is insufficient.
   - deterministic insufficiency outcomes.

2. Evidence-pack hygiene:
   - deterministic evidence IDs and metadata.
   - citeable-only context.
   - dedupe and score-priority ordering.

3. Structured schema output:
   - `answer`, `claims[]`, `citations[]`, `insufficiency_reason`, `confidence`, `grounding_status`.
   - bounded schema retry guard.

4. Citation and scope validation:
   - claim-to-citation linkage.
   - `tenant_id + app_id + collection_id` scope checks.
   - ACL overlap checks.
   - span-level citation support when span metadata exists.

5. Abstention/partial policy:
   - configurable confidence/coverage thresholds.
   - deterministic abstain or partial behavior.

6. Contradiction handling:
   - policy modes: abstain, summarize disagreement, prefer newest, prefer highest authority.

7. Optional verifier pass:
   - outcomes: `pass`, `revise`, `reject`.
   - bounded attempt and elapsed-time limits.

8. Prompt-injection defense:
   - retrieved content treated as untrusted.
   - sanitizer marks/redacts known instruction-injection patterns.
   - injected output attempts are rejected.

9. Diagnostics and audit metadata:
   - response includes scope-safe diagnostics and governance outcomes.

## Conversation Memory Grounding Policy

- Memory, summary, and user facts are contextual by default and non-citeable.
- Optional mode allows memory citation only when memory is represented as retrieved evidence in the current evidence pack.
- Memory text not present as retrieved evidence cannot satisfy grounded citation requirements.

## Phase 3.4 Verification Coverage

1. Unit verification:
   - summary generation prefers recent turns and normalizes output.
   - prompt assembly budget behavior is deterministic and priority-based.
   - multi-hop and structural diff selection are verified in retrieval service tests.

2. Integration verification:
   - provider orchestration is validated end-to-end through `IConversationService.GenerateReplyAsync`.
   - assistant turn persistence and confidence shaping are validated in database-backed flow.
   - retrieval response shaping validates provenance fields and extended metadata projection (`document_group_id`, `version_number`, `referenced_node_ids`).

3. Token reduction guarantee:
   - summary-first assembly policy is tested to ensure history is dropped before required context when budget is constrained.
