namespace TrueRag.Ingestion.Models;

public sealed record NormalizedIngestionDocument(
    string DocumentId,
    string DocumentGroupId,
    string VersionNumber,
    string CollectionId,
    FidelityLevel FidelityLevel,
    IReadOnlyCollection<string> AllowedDocumentGroups,
    IReadOnlyCollection<NormalizedNode> Nodes);

public sealed record NormalizedNode(
    string NodeId,
    string NodeType,
    string Text,
    float[] Vector,
    string? ParentId,
    string? LogicalPath,
    NormalizedBoundingBox? BoundingBox,
    IReadOnlyCollection<string> ReferencedNodeIds,
    bool HasHierarchyMetadata,
    bool HasProvenanceMetadata,
    bool HasTableMetadata);

public sealed record NormalizedBoundingBox(
    int Page,
    float X,
    float Y,
    float W,
    float H);
