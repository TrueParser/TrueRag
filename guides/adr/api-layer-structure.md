# ADR 014: API Layer Structure and Composition Boundary

## Status
Proposed

## Context
The current TrueRAG HTTP surface is functional, but enterprise maintainability requires a stricter API-layer composition model with explicit separation of concerns.

Requirements from architecture and AGENTS constraints:
- Keep `TrueRag.Host` as the only executable entrypoint.
- Keep bounded modules independent; no shared junk-drawer project.
- Enforce tenant/app request scope deterministically before business execution.
- Keep controllers small and focused on transport concerns.
- Centralize cross-cutting concerns (exception handling, correlation, guardrails) as middleware.

## Decision
Adopt a structured API module layout in `TrueRag.Api`:

- `Controllers/`
  - HTTP routes and transport binding only.
  - No business logic.
  - Health probe controller is mandatory (`/health/live`, `/health/ready`).

- `Models/`
  - API request/response contracts specific to HTTP boundary.

- `Services/`
  - API orchestration adapters that coordinate use-cases for controllers.
  - Convert core results to API-friendly output DTOs if needed.

- `Helpers/`
  - Shared API helpers (result/problem mapping, envelope shaping).

- `Extensions/`
  - Service registration and app-pipeline composition extension methods.

- `Middleware/`
  - Cross-cutting pipeline: tenant/app scope guard, exception handling, correlation, admission/resource guards.

- `ResourceGuard/` (optional, when phase 3.5 is implemented)
  - Node-safety admission and overload shed components at API boundary.

- `Workers/`
  - Not hosted in `TrueRag.Api` process; if folder exists, it only holds API-facing coordination contracts and must not create another executable.

## Composition Rules
1. `TrueRag.Host` composes `TrueRag.Api` and maps controllers through `MapControllers()`.
2. Request context middleware executes before controller dispatch.
3. Tenant/app guard middleware executes early and fails fast on missing/invalid scope.
4. Global exception middleware emits consistent error contracts.
5. Controllers depend on interfaces/services; storage/retrieval internals remain outside API layer.
6. Health endpoints stay anonymous and independent from tenant/app scope:
   - `GET /health/live` for process liveness.
   - `GET /health/ready` for dependency readiness; return `503` when critical dependencies are unavailable.

## Consequences
### Positive
- Predictable, enterprise-grade API boundary.
- Reduced controller bloat and duplicate response/error shaping logic.
- Better testability of HTTP behavior vs orchestration behavior.
- Cleaner path for phase 3.5 middleware hardening.

### Negative
- More files and explicit wiring compared to minimal endpoints.
- Requires migration effort from inline route handlers and ad-hoc response handling.

## Rollout Notes
- Apply incrementally without changing public routes.
- Preserve route and payload backward compatibility.
- Add tests for middleware order and tenant/app fail-fast behavior before marking completion.
- Add integration tests for `live` and `ready` behavior with at least one dependency-failure readiness case.
