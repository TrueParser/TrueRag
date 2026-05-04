# ADR 016: Schema Migrations and Bootstrap Strategy

## Status
Accepted

## Context
TrueRAG deploys as a modular monolith with multiple bounded-context modules sharing database infrastructure patterns across CrateDB/PostgreSQL-compatible engines.  
New node/environment bootstrapping must be deterministic, safe, and idempotent without risking existing data.

The platform requires:
- repeatable schema creation for fresh environments
- safe forward migrations for running environments
- no destructive rewrites by default
- explicit operational control in production

## Decision
1. **Manual-first migration execution**
- Production uses an explicit migration command/workflow (`migrate up`) as the default.
- Automatic startup migration is optional and disabled by default in production.

2. **Optional startup automation for non-prod**
- Development/test environments may enable startup migration for convenience.
- Startup automation must use the same migration engine and history tracking as manual execution.

3. **Idempotent migration model**
- Maintain a migration history table (for example `schema_migrations`).
- Migrations are append-only and versioned.
- Use guarded DDL (`IF NOT EXISTS`/equivalent) where supported.
- Forward-only by default; destructive changes require explicit, separate operator-approved tasks.

4. **Dual-engine compatibility**
- Migration artifacts support CrateDB and PostgreSQL differences via engine-aware scripts/executors.
- Validation ensures required schema is present for selected runtime engine before serving traffic.

5. **Operational commands**
- `migrate up` applies pending migrations.
- `migrate status` lists applied/pending versions.
- `migrate validate` confirms runtime schema compatibility.

6. **Runtime safety**
- If required migrations are pending and auto-migrate is disabled, host startup fails fast with clear diagnostics.
- Migration execution emits structured logs and metrics for auditability.

## Consequences
### Positive
- Reliable bootstrap for new nodes/environments.
- Safe, controlled production schema evolution.
- Clear operational visibility into schema state.

### Negative
- Additional tooling and governance overhead.
- Requires disciplined migration authoring and review across modules.
