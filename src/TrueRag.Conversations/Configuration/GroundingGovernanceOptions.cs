namespace TrueRag.Conversations.Configuration;

public sealed class GroundingGovernanceOptions
{
    public const string SectionName = "GroundingGovernance";

    public double MinimumRetrievalConfidence { get; set; } = 0.45d;

    public double MinimumEvidenceCoverage { get; set; } = 0.75d;

    public bool RequireCitationCompleteness { get; set; } = true;

    public bool AllowPartialAnswer { get; set; } = true;

    public double MinimumCoverageForPartialAnswer { get; set; } = 0.40d;

    public ConflictResolutionPolicy ConflictPolicy { get; set; } = ConflictResolutionPolicy.SummarizeDisagreement;

    public bool EnableVerifierPass { get; set; } = false;

    public int VerifierMaxAttempts { get; set; } = 1;

    public int VerifierMaxElapsedMs { get; set; } = 800;

    public ConversationMemoryCitationPolicy MemoryCitationPolicy { get; set; } = ConversationMemoryCitationPolicy.NonCiteable;
}

public enum ConflictResolutionPolicy
{
    Abstain = 0,
    SummarizeDisagreement = 1,
    PreferNewest = 2,
    PreferHighestAuthority = 3
}

public enum ConversationMemoryCitationPolicy
{
    NonCiteable = 0,
    CiteableWhenRetrievedEvidence = 1
}
