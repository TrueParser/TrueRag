using TrueRag.Core.Models;

namespace TrueRag.Core.Abstractions;

public interface IEmbeddingProfileGovernanceService
{
    Task<EmbeddingProfileTransitionRecord> ProposeTransitionAsync(
        EmbeddingProfileTransitionProposal proposal,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileTransitionRecord?> GetPendingTransitionAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileMigrationReadiness> GetMigrationReadinessAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileTransitionRecord> ActivateTransitionAsync(
        string transitionId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileTransitionRecord> MarkReembeddingCompletedAsync(
        string transitionId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileTransitionRecord> RollbackTransitionAsync(
        string transitionId,
        CancellationToken cancellationToken = default);
}

