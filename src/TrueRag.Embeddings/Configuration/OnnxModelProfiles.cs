using TrueRag.Core.Models;

namespace TrueRag.Embeddings.Configuration;

public sealed class OnnxModelProfile
{
    public required string Name { get; init; }

    public required string ModelId { get; init; }

    public required int Dimensions { get; init; }

    public required int MaxTokens { get; init; }

    public required EmbeddingDistanceMetric DistanceMetric { get; init; }

    public string? ModelArtifactsPath { get; init; }

    public string? ModelFileName { get; init; }

    public string? ChecksumSha256 { get; init; }
}

public sealed class EmbeddingScopeProfileSelection
{
    public required string TenantId { get; init; }

    public required string AppId { get; init; }

    public required string CollectionId { get; init; }

    public required string ProfileName { get; init; }
}

public sealed class OnnxProfileSelectionOptions
{
    public const string SectionName = "Embeddings:ProfileSelection";

    public string DefaultProfileName { get; set; } = "bge-small-en-v1.5";

    public int? ExpectedVectorDimensions { get; set; }

    public int? ExpectedVectorMaxTokens { get; set; }

    public bool AllowGlobalPathFallback { get; set; } = true;

    public List<EmbeddingScopeProfileSelection> ScopedProfiles { get; set; } = [];
}
