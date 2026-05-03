namespace TrueRag.Core.Models;

public sealed record RetrievalQuery(
    string QueryText,
    float[]? QueryVector,
    int TopK,
    IReadOnlyDictionary<string, string>? Filters = null);

public sealed record StructuralExpansionSeed(
    string DocumentId,
    string SectionPathPrefix);

public sealed record AdjacentExpansionSeed(
    string DocumentId,
    string AnchorNodeId);

public sealed record RetrievedNode(
    string NodeId,
    string DocumentId,
    string NodeType,
    string Text,
    double Score,
    string FidelityLevel,
    int? PageNumber,
    BoundingBoxDto? BoundingBox,
    string? LogicalPath,
    RetrievalProvenance? Provenance = null);

public sealed record RetrievalProvenance(
    int? PageNumber,
    BoundingBoxDto? BoundingBox,
    string? LogicalPath);

public sealed record RetrievalResponse(
    IReadOnlyCollection<RetrievedNode> Nodes);
