namespace TrueRag.Retrieval.Configuration;

public sealed class RetrievalEngineOptions
{
    public const string SectionName = "RetrievalEngine";

    public bool RequireHighFidelity { get; set; }

    public bool FallbackToStandardRag { get; set; } = true;

    public bool EnableMultiHopLinking { get; set; } = true;

    public int MultiHopMaxNodes { get; set; } = 16;

    public bool EnableStructuralDiffing { get; set; } = true;

    public int StructuralDiffMaxRequests { get; set; } = 4;

    public bool EnableSemanticCache { get; set; } = true;

    public TimeSpan SemanticCacheTtl { get; set; } = TimeSpan.FromMinutes(2);

    public bool EnableDistributedRateLimit { get; set; } = false;

    public int DistributedRateLimitRequests { get; set; } = 120;

    public TimeSpan DistributedRateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);

    public double RetrievalConfidenceWeight { get; set; } = 0.7;

    public double LlmCertaintyWeight { get; set; } = 0.3;
}
