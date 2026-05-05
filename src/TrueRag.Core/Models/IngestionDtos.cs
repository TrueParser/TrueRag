namespace TrueRag.Core.Models;

public sealed record IngestionRequestDto(
    string DocumentId,
    string DocumentGroupId,
    string VersionNumber,
    IReadOnlyCollection<string> AllowedDocumentGroups,
    string Fidelity,
    IReadOnlyCollection<ChunkDto> Chunks,
    string? CollectionId = null,
    string? EmbeddingModeTag = null,
    string? PrecomputedEmbeddingProvider = null,
    string? PrecomputedEmbeddingModel = null);

public sealed record ChunkDto(
    string Id,
    string? ParentId,
    string? LogicalPath,
    string Type,
    string Text,
    BoundingBoxDto? BoundingBox,
    IReadOnlyCollection<string>? ReferencedNodeIds,
    float[] Vector);

public sealed record BoundingBoxDto(
    int Page,
    float X,
    float Y,
    float W,
    float H);
