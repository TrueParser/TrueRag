# TrueRAG Host

The single executable composition root for TrueRAG.

Responsibilities:

- load and own `appsettings*.json`
- configure host-level dependency injection
- wire middleware, routing, and hosting
- register module libraries such as API, Workers, Retrieval, Ingestion, Storage, and Conversations
- serve as the only deployable binary for the modular monolith
