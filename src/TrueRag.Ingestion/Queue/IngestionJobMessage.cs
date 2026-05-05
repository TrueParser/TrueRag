namespace TrueRag.Ingestion.Queue;

public sealed record IngestionJobMessage(
    string NodeId,
    string TenantId,
    string AppId,
    string CollectionId,
    string? UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> AllowedDocumentGroups,
    string WalPath,
    string WalSegmentId,
    long WalOffset,
    long WalLength,
    bool RequiresInternalEmbeddingGeneration = false,
    bool UsesPrecomputedVectors = true);
