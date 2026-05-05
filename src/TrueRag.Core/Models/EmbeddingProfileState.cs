namespace TrueRag.Core.Models;

public sealed record ActiveEmbeddingProfileRecord(
    string TenantId,
    string AppId,
    string CollectionId,
    string Provider,
    string Model,
    int Dimensions,
    int MaxTokens,
    EmbeddingDistanceMetric DistanceMetric,
    string? Version,
    string? Checksum,
    DateTimeOffset ActivatedAtUtc);

public sealed record EmbeddingProfileCompatibilityResult(
    bool IsCompatible,
    string? Reason = null);

public enum EmbeddingProfileTransitionStatus
{
    Proposed = 0,
    ReadyForActivation = 1,
    Activated = 2,
    RolledBack = 3
}

public sealed record EmbeddingProfileTransitionProposal(
    string TenantId,
    string AppId,
    string CollectionId,
    string TargetProvider,
    string TargetModel,
    int TargetDimensions,
    int TargetMaxTokens,
    EmbeddingDistanceMetric TargetDistanceMetric,
    bool RequiresReembedding,
    bool ReembeddingCompleted,
    string? TargetVersion = null,
    string? TargetChecksum = null,
    string? Notes = null);

public sealed record EmbeddingProfileTransitionRecord(
    string TransitionId,
    string TenantId,
    string AppId,
    string CollectionId,
    string? SourceProvider,
    string? SourceModel,
    int? SourceDimensions,
    string TargetProvider,
    string TargetModel,
    int TargetDimensions,
    int TargetMaxTokens,
    EmbeddingDistanceMetric TargetDistanceMetric,
    string? TargetVersion,
    string? TargetChecksum,
    bool RequiresReembedding,
    bool ReembeddingCompleted,
    EmbeddingProfileTransitionStatus Status,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record EmbeddingProfileMigrationReadiness(
    bool IsReady,
    string Reason,
    ActiveEmbeddingProfileRecord? CurrentProfile,
    EmbeddingProfileTransitionRecord? PendingTransition);
