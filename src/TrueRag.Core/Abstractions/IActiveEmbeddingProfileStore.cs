using TrueRag.Core.Models;

namespace TrueRag.Core.Abstractions;

public interface IActiveEmbeddingProfileStore
{
    Task<ActiveEmbeddingProfileRecord?> GetActiveAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default);

    Task UpsertActiveAsync(
        ActiveEmbeddingProfileRecord profile,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ActiveEmbeddingProfileRecord>> GetActivationHistoryAsync(
        string tenantId,
        string appId,
        string collectionId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileCompatibilityResult> CheckCompatibilityAsync(
        string tenantId,
        string appId,
        string collectionId,
        string provider,
        string model,
        int dimensions,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileTransitionRecord> CreateTransitionAsync(
        EmbeddingProfileTransitionProposal proposal,
        ActiveEmbeddingProfileRecord? currentProfile,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileTransitionRecord?> GetTransitionByIdAsync(
        string transitionId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingProfileTransitionRecord?> GetLatestPendingTransitionAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default);

    Task UpdateTransitionAsync(
        EmbeddingProfileTransitionRecord transition,
        CancellationToken cancellationToken = default);
}
