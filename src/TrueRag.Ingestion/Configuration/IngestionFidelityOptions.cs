namespace TrueRag.Ingestion.Configuration;

public sealed class IngestionFidelityOptions
{
    public const string SectionName = "IngestionFidelity";

    public string DefaultMode { get; set; } = "auto";

    public bool AllowExplicitOverride { get; set; } = true;
}