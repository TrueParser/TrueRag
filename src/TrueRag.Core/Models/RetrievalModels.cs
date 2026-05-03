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
    RetrievalProvenance? Provenance = null,
    string? DocumentGroupId = null,
    string? VersionNumber = null,
    IReadOnlyCollection<string>? ReferencedNodeIds = null);

public sealed record RetrievalProvenance(
    int? PageNumber,
    BoundingBoxDto? BoundingBox,
    string? LogicalPath);

public sealed record RetrievalResponse(
    IReadOnlyCollection<RetrievedNode> Nodes,
    double? RetrievalConfidence = null,
    double? LlmCertainty = null,
    double? OverallConfidence = null,
    IReadOnlyCollection<StructuralDiffResult>? Diffs = null);

public sealed record StructuralDiffRequest(
    string DocumentGroupId,
    string LeftVersion,
    string RightVersion,
    string LogicalPath);

public sealed record StructuralDiffResult(
    string DocumentGroupId,
    string LeftVersion,
    string RightVersion,
    string LogicalPath,
    string LeftText,
    string RightText,
    string UnifiedDiff);
