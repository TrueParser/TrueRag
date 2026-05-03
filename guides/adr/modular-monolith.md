# ADR 003: Core Architecture - Modular Monolith

## Status
Proposed

## Context
The TrueRAG API needs to serve as a robust backend for production use while also being a developer-friendly reference implementation/marketing showcase for TrueParser. 

## Decision
We will build the application as a **Modular Monolith** using ASP.NET Core 8+.
- **Modules:** The system is split into logical modules (e.g., Ingestion, Storage, Conversations) represented as separate .NET Class Libraries within a single `.sln`.
- **Deployment:** The application deploys as a single executable/container.
- **Inter-module Communication:** Modules communicate via standard C# interfaces and Dependency Injection, not via HTTP/gRPC networks.

## Consequences
### Positive
- **Developer Experience:** Extremely easy to clone, build, and run locally (`F5` experience).
- **Performance:** In-memory method calls between the Core Search module and Conversation module have near-zero latency.
- **Refactoring:** Easy to refactor boundaries using standard IDE tools before eventually breaking out into microservices (if ever needed).

### Negative
- If the Conversation module requires heavy scaling independently of the Ingestion module, a monolith cannot scale them asymmetrically out-of-the-box.
- Potential for "Big Ball of Mud" if strict project reference rules (Clean Architecture) are not enforced via architecture tests.
