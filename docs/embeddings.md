# TrueRag.Embeddings

## Responsibilities
- Hosts embedding-provider implementations behind `TrueRag.Core` abstractions.
- Registers embedding provider infrastructure through `AddTrueRagEmbeddings`.
- Enforces provider lookup behavior using `IEmbeddingProviderRegistry`.
- Resolves active embedding descriptor/profile by effective scope (`tenant_id + app_id + collection_id`).
- Resolves collection embedding mode (internal vs external).

## API Surface
- `EmbeddingsModule.AddTrueRagEmbeddings(IServiceCollection)`.
- Internal registry implementation for provider resolution.
- `IEmbeddingProvider` implementations:
  - Internal ONNX provider family (priority path)
  - External OpenAI provider (first external provider)
- `IEmbeddingProfileResolver`
- `ICollectionEmbeddingModeResolver`
- `IQueryEmbeddingGenerator`

## Boundaries
- Depends on `TrueRag.Core` contracts only.
- Must not own host process startup or runtime entrypoint configuration.
- Provider SDK integrations remain isolated to this module and must not leak into `TrueRag.Core`.

## Execution Modes
- Sync client-managed mode:
  - ingestion sync requires client-precomputed chunk vectors
  - retrieval requires caller-provided query vectors for vector/hybrid lanes
- Async pipeline-managed mode:
  - ingestion async generates embeddings in worker orchestration
  - provider can be internal ONNX or configured external adapter
  - retrieval generates query vectors from query text
  - OpenAI is the first enabled external adapter in production path
  - provider selection is driven by active scoped descriptor and registry lookup
  - batch responses are mapped deterministically to chunks (including OpenAI index ordering)

## Deterministic Safety Rules
- Embedding-space compatibility is enforced via active descriptor dimensions.
- Ingestion mismatch error: `ingestion.embedding_space_mismatch`.
- Retrieval mismatch error: `retrieval.embedding_space_mismatch`.
