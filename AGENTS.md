# AI Agents Guidelines

This document provides context and guidelines for AI agents (like GitHub Copilot, Cursor, or Gemini) assisting with the TrueRAG repository.

## Role
You are assisting in building TrueRAG, a High-Performance Modular Monolith **.NET 10** API for Retrieval-Augmented Generation (RAG). 

## Project Context
TrueRAG serves as the reference architecture and marketing showcase for **TrueParser** (a High Fidelity Parsing Engine). 
The core philosophy is to leverage TrueParser's *structural fidelity* (preserving tables, document hierarchy, paragraphs) rather than blindly chunking text.

## Core Documentation References
Before writing code or making architectural changes, review the following documentation:
* [Product Vision & Differentiators](guides/Product-Vision.md): The 4 killer features of TrueRAG (Structural Expansion, Provenance, Table-Awareness, Document Scoping).
* [Project Scope](guides/Scope.md): Details on what is strictly In-Scope vs. Out-of-Scope.
* [System Architecture](guides/Architecture.md): High-level Modular Monolith and CrateDB design.
* [WAL Architecture](guides/WAL-Architecture.md): Write-Ahead Log mechanics for high-TPS asynchronous ingestion.
* [NATS Architecture](guides/NATS-Architecture.md): Node-scoped event orchestration for background workers.
* [System Invariants](guides/INVARIANTS.md): Absolute architectural rules (Redis boundaries, WAL safety, etc).
* [Data Permissions](guides/Permissions.md): Namespace isolation and Document-Level ACL filtering.
* [Ingestion Contract](guides/Ingestion-Contract.md): Canonical JSON payload and C# DTO boundaries.
 * [Repository Structure](guides/Repo%20Structure.md): Standard layout for the `.NET` solution.
 * [Application Host](docs/host.md): Single-binary composition root, configuration owner, and startup boundary.
 * [API Module Doc](docs/api.md): Controller routes, scope guard behavior, and endpoint contracts.
 * [Conversations Module Doc](docs/conversations.md): Prompt assembly, grounding governance, verifier behavior, and conversation-memory citation policy.
 * [Ingestion Module Doc](docs/ingestion.md): Sync/async ingestion responsibilities, WAL/queue behavior, and embedding execution rules.
 * [Retrieval Module Doc](docs/retrieval.md): Search lane behavior, scope enforcement, and query embedding mode requirements.
 * [Embeddings Module Doc](docs/embeddings.md): Provider pipeline contracts, mode resolution, and descriptor compatibility rules.
* **ADRs (Architecture Decision Records):**
  * [ADR 001: Hybrid Search](guides/adr/hybrid-search.md)
  * [ADR 002: Conversation Memory](guides/adr/conversation-memory.md)
  * [ADR 003: Modular Monolith](guides/adr/modular-monolith.md)
  * [ADR 004: Ingestion & Storage](guides/adr/ingestion-storage.md)
  * [ADR 005: Advanced Retrieval Engine](guides/adr/retrieval-engine.md)
  * [ADR 006: Fidelity Profiles (Graceful Degradation)](guides/adr/fidelity-profiles.md)
  * [ADR 007: Async Ingestion (NATS)](guides/adr/async-ingestion-nats.md)
  * [ADR 008: Stateless Retrieval API (Redis)](guides/adr/stateless-retrieval-redis.md)
* [ADR 009: Multi-Hop Document Linking](guides/adr/multi-hop-linking.md)
  * [ADR 010: Structural Diffing & Version-Aware RAG](guides/adr/structural-diffing.md)
  * [ADR 011: Dual-Layer Confidence Scoring](guides/adr/confidence-scoring.md)
  * [ADR 012: Auth-Agnostic Core & Logical Multi-Tenancy](guides/adr/multi-tenancy-auth.md)
  * [ADR 013: Node-Safety Admission and Backpressure Control](guides/adr/node-safety-admission-backpressure.md)
  * [ADR 022: Hallucination Governance and Grounded Generation](guides/adr/hallucination-governance-grounded-generation.md)

* **Reference Code:** When implementing WAL, queueing, or node-scoped worker behavior, use the code under `reference-code/Queue` and `reference-code/Wal` as the behavioral reference source. Treat it as read-only reference material unless a task explicitly says otherwise.
* **Quality Gate Assets:** For hallucination-governance quality gates, use `tests/quality/hallucination-eval-cases.json` and `tests/quality/Invoke-HallucinationEval.ps1` as the canonical evaluation baseline and threshold gate.

## Rules for Agents
0. **Mandatory First Step (Per Task):** At the start of **every** task/session, the agent must first read `AGENTS.md` and treat it as the active execution contract before making any code changes.
1. **Architecture Compliance:** Maintain the **Enterprise Modular Monolith** structure. Do not introduce microservices or separate standalone APIs. Furthermore, **strictly forbid** the creation of generic "junk drawer" projects like `TrueRag.Infrastructure` or `TrueRag.Shared`. All infrastructure must live within its specific bounded context module (`Storage`, `Ingestion`, etc). The only executable entrypoint is `TrueRag.Host`; `TrueRag.Api` and `TrueRag.Workers` are module libraries under that host. Module boundaries must be strictly enforced via interfaces and Dependency Injection.
2. **Proper Testing Implementation:** All business logic must have isolated Unit Tests. Any interactions with CrateDB or LLMs must be covered by comprehensive Integration Tests (preferably utilizing Testcontainers to ensure clean, repeatable database states).
3. **Data Store:** The primary database is CrateDB / PostgreSQL. Favor SQL-based Hybrid Search approaches natively supported by CrateDB.
4. **No Dumb Chunking:** Remember that standard token-based chunking, embedding generation, and re-ranking are out of scope for the core engine (handled upstream or by TrueParser). 
5. **Performance:** Prioritize low-latency and **C# 14+ / .NET 10** performance features.
6. **Code Style:** Follow standard enterprise .NET conventions and strictly adhere to the project's `.editorconfig`.
7. **Execution Governance:** Any changes to the codebase require an explicit Implementation Plan. Furthermore, all work must be tracked as explicit tasks broken down into Phases within a `TASK.md` file. Agents must strictly only execute code changes that correspond to the listed Tasks in `TASK.md`.
8. **Module Documentation:** Every time a new bounded context or feature module (e.g., `TrueRag.Ingestion`) is created, the agent **must** create a corresponding markdown file in the `docs/` directory detailing its API surface and responsibilities. These will eventually be consolidated into a single developer guide.
9. **Host Configuration:** Single-binary deployment means appsettings, configuration binding, and runtime composition belong in `TrueRag.Host`. Module projects may expose configuration options, but they must not own the process entrypoint.
10. **ADR and Invariants are Hard Gates:** `guides/adr/*`, `guides/INVARIANTS.md`, `guides/WAL-Architecture.md`, and `guides/NATS-Architecture.md` are **mandatory acceptance criteria**. Treat them as requirements, not suggestions.
11. **No Partial-Compliance Closure:** A task must not be marked complete in `TASK.md` if implementation is only partially aligned with ADR/invariant requirements. Partial work must remain unchecked and explicitly tracked as follow-up tasks.
12. **Explicit Compliance Reporting:** For each completed task slice, the agent must report: (a) what was implemented, (b) which ADR/invariant clauses are satisfied, and (c) which clauses remain open.
