namespace TrueRag.Embeddings.Configuration;

public sealed class EmbeddingModeScopeSelection
{
    public required string TenantId { get; init; }

    public required string AppId { get; init; }

    public required string CollectionId { get; init; }

    public required string Mode { get; init; }
}

public sealed class EmbeddingModeSelectionOptions
{
    public const string SectionName = "Embeddings:ModeSelection";

    public string DefaultMode { get; set; } = "internal_embedding";

    public List<EmbeddingModeScopeSelection> ScopedModes { get; set; } = [];
}
