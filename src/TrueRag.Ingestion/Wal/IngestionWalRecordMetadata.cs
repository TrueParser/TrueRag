namespace TrueRag.Ingestion.Wal;

public sealed record IngestionWalRecordMetadata(
    string TenantId,
    string AppId,
    string CollectionId,
    string DocumentId,
    string CorrelationId,
    string NodeId,
    bool RequiresInternalEmbeddingGeneration = false,
    bool UsesPrecomputedVectors = true);
