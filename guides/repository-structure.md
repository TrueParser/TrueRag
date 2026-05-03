# TrueRAG Repository Structure

This is the legacy mirror of the canonical layout described in [guides/Repo Structure.md](Repo%20Structure.md). The repo is a modular monolith with a single executable host and feature modules beneath it.

```text
TrueRag/
├─ src/
│  ├─ TrueRag.Host/            # Single executable composition root (appsettings, startup, hosting)
│  ├─ TrueRag.Api/             # HTTP/API module (route mapping, request validation, DI extensions)
│  ├─ TrueRag.Core/            # Domain entities, interfaces, exceptions, shared enums
│  ├─ TrueRag.Ingestion/       # Document ingestion pipelines, TrueParser contract mappers
│  ├─ TrueRag.Retrieval/       # Search, hybrid retrieval, multi-hop, diffing, confidence
│  ├─ TrueRag.Storage/         # CrateDB/PostgreSQL implementations, SQL, repositories
│  ├─ TrueRag.Conversations/   # LLM orchestration, memory, threads, prompts
│  └─ TrueRag.Workers/         # Background workers, WAL replay, pruning, hosted services
├─ tests/
│  ├─ TrueRag.UnitTests/
│  └─ TrueRag.IntegrationTests/
├─ contracts/
├─ docs/
├─ guides/
├─ reference-code/
├─ tools/
├─ deploy/
├─ samples/
├─ Directory.Build.props
├─ Directory.Packages.props
└─ TrueRag.sln
```

## Layer Constraints

### Central Package Management
- All NuGet package versions are managed centrally via `Directory.Packages.props`.
- Project files reference packages without version numbers.

### Feature Modules, Not Generic Infra
- Do not introduce a generic `TrueRag.Infrastructure` or `TrueRag.Shared` project.
- `Storage`, `Ingestion`, `Retrieval`, `Conversations`, and `Workers` remain bounded modules with concrete responsibilities.

### Core Foundation
- `TrueRag.Core` is the shared foundation for contracts, request context, domain models, and abstractions.
- It must not reference database-specific packages or other feature modules.

### Orchestration
- `TrueRag.Host` is the only executable entrypoint.
- `TrueRag.Api` and `TrueRag.Workers` are module libraries loaded by the host.
