using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;

namespace TrueRag.Embeddings;

internal sealed class EmbeddingProfileGovernanceService(IActiveEmbeddingProfileStore store) : IEmbeddingProfileGovernanceService
{
    public async Task<EmbeddingProfileTransitionRecord> ProposeTransitionAsync(
        EmbeddingProfileTransitionProposal proposal,
        CancellationToken cancellationToken = default)
    {
        var current = await store.GetActiveAsync(proposal.TenantId, proposal.AppId, proposal.CollectionId, cancellationToken);
        return await store.CreateTransitionAsync(proposal, current, cancellationToken);
    }

    public Task<EmbeddingProfileTransitionRecord?> GetPendingTransitionAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default)
        => store.GetLatestPendingTransitionAsync(tenantId, appId, collectionId, cancellationToken);

    public async Task<EmbeddingProfileMigrationReadiness> GetMigrationReadinessAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        var current = await store.GetActiveAsync(tenantId, appId, collectionId, cancellationToken);
        var pending = await store.GetLatestPendingTransitionAsync(tenantId, appId, collectionId, cancellationToken);

        if (pending is null)
        {
            return new EmbeddingProfileMigrationReadiness(true, "no_pending_transition", current, null);
        }

        if (pending.RequiresReembedding && !pending.ReembeddingCompleted)
        {
            return new EmbeddingProfileMigrationReadiness(false, "reembedding_not_completed", current, pending);
        }

        return new EmbeddingProfileMigrationReadiness(true, "ready_for_activation", current, pending);
    }

    public async Task<EmbeddingProfileTransitionRecord> MarkReembeddingCompletedAsync(
        string transitionId,
        CancellationToken cancellationToken = default)
    {
        var transition = await store.GetTransitionByIdAsync(transitionId, cancellationToken)
            ?? throw new InvalidOperationException($"Transition '{transitionId}' not found.");

        if (transition.Status is EmbeddingProfileTransitionStatus.Activated or EmbeddingProfileTransitionStatus.RolledBack)
        {
            throw new InvalidOperationException("Cannot mark re-embedding on finalized transition.");
        }

        transition = transition with
        {
            ReembeddingCompleted = true,
            Status = EmbeddingProfileTransitionStatus.ReadyForActivation,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.UpdateTransitionAsync(transition, cancellationToken);
        return transition;
    }

    public async Task<EmbeddingProfileTransitionRecord> ActivateTransitionAsync(
        string transitionId,
        CancellationToken cancellationToken = default)
    {
        var transition = await store.GetTransitionByIdAsync(transitionId, cancellationToken)
            ?? throw new InvalidOperationException($"Transition '{transitionId}' not found.");

        if (transition.Status is EmbeddingProfileTransitionStatus.Activated or EmbeddingProfileTransitionStatus.RolledBack)
        {
            throw new InvalidOperationException("Transition is already finalized.");
        }

        if (transition.RequiresReembedding && !transition.ReembeddingCompleted)
        {
            throw new InvalidOperationException("Transition cannot be activated before re-embedding is completed.");
        }

        var active = new ActiveEmbeddingProfileRecord(
            transition.TenantId,
            transition.AppId,
            transition.CollectionId,
            transition.TargetProvider,
            transition.TargetModel,
            transition.TargetDimensions,
            transition.TargetMaxTokens,
            transition.TargetDistanceMetric,
            transition.TargetVersion,
            transition.TargetChecksum,
            DateTimeOffset.UtcNow);

        await store.UpsertActiveAsync(active, cancellationToken);
        transition = transition with { Status = EmbeddingProfileTransitionStatus.Activated, UpdatedAtUtc = DateTimeOffset.UtcNow };
        await store.UpdateTransitionAsync(transition, cancellationToken);
        return transition;
    }

    public async Task<EmbeddingProfileTransitionRecord> RollbackTransitionAsync(
        string transitionId,
        CancellationToken cancellationToken = default)
    {
        var transition = await store.GetTransitionByIdAsync(transitionId, cancellationToken)
            ?? throw new InvalidOperationException($"Transition '{transitionId}' not found.");

        if (transition.SourceProvider is null || transition.SourceModel is null || !transition.SourceDimensions.HasValue)
        {
            throw new InvalidOperationException("Rollback unavailable: transition has no source profile snapshot.");
        }

        var rollback = new ActiveEmbeddingProfileRecord(
            transition.TenantId,
            transition.AppId,
            transition.CollectionId,
            transition.SourceProvider,
            transition.SourceModel,
            transition.SourceDimensions.Value,
            transition.TargetMaxTokens,
            transition.TargetDistanceMetric,
            transition.TargetVersion,
            transition.TargetChecksum,
            DateTimeOffset.UtcNow);

        await store.UpsertActiveAsync(rollback, cancellationToken);
        transition = transition with { Status = EmbeddingProfileTransitionStatus.RolledBack, UpdatedAtUtc = DateTimeOffset.UtcNow };
        await store.UpdateTransitionAsync(transition, cancellationToken);
        return transition;
    }
}

