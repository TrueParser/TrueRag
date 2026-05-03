# Repository Structure

The TrueRAG repository follows Clean Architecture principles, tailored for a modular monolith. 

```text
TrueRag/
├── src/
│   ├── TrueRag.Host/            # Single executable composition root (appsettings, startup, hosting)
│   ├── TrueRag.Api/                 # HTTP/API module (route mapping, request validation, DI extensions)
│   ├── TrueRag.Core/                # Domain Entities, Interfaces, Exceptions, Shared Enums
│   ├── TrueRag.Ingestion/           # Document ingestion pipelines, TrueParser contract mappers
│   ├── TrueRag.Storage/             # CrateDB/Npgsql implementations, Hybrid Search queries, DbContexts
│   └── TrueRag.Conversations/       # LLM Orchestration, Memory, Threads, Prompts
├── tests/
│   ├── TrueRag.UnitTests/           # Isolated unit tests for business logic
│   └── TrueRag.IntegrationTests/    # Database and LLM integration tests
├── docs/
│   ├── adr/                         # Architecture Decision Records
│   ├── Architecture.md
│   ├── Scope.md
│   └── Repo Structure.md
├── AGENTS.md                        # AI Agent guidelines
└── TrueRag.sln                      # .NET Solution File
```

## Project Responsibilities

* **`TrueRag.Api`**: Handles HTTP requests, authentication, request validation, and wiring up dependency injection. Should contain almost no business logic and is loaded by `TrueRag.Host`.
* **`TrueRag.Host`**: The single deployable entrypoint. Owns `appsettings*.json`, host-level DI composition, middleware wiring, and module bootstrap.
* **`TrueRag.Core`**: The heart of the application. Contains domain models (e.g., `DocumentNode`, `ConversationThread`) and interfaces (`IRetrievalService`, `IRetrievalRepository`, `ILlmClient`). Has no external dependencies.
* **`TrueRag.Storage`**: Implements the interfaces defined in Core. Deals with CrateDB, PostgreSQL drivers, SQL generation, and vector index management.
* **`TrueRag.Ingestion`**: Handles the intake of TrueParser JSON. Validates and normalizes the high-fidelity structural data before passing it to Storage.
* **`TrueRag.Conversations`**: Manages the stateful nature of RAG. Handles saving thread history, constructing LLM prompts with injected context, and parsing LLM responses.
