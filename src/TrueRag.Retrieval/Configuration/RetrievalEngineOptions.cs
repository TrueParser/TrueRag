namespace TrueRag.Retrieval.Configuration;

public sealed class RetrievalEngineOptions
{
    public const string SectionName = "RetrievalEngine";

    public bool RequireHighFidelity { get; set; }

    public bool FallbackToStandardRag { get; set; } = true;
}
