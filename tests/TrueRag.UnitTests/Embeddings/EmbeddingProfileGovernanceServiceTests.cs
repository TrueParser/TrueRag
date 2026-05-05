using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;
using TrueRag.Embeddings;

namespace TrueRag.UnitTests.Embeddings;

public sealed class EmbeddingProfileGovernanceServiceTests
{
    [Fact]
    public async Task ActivateTransitionAsync_Rejects_WhenReembeddingRequiredAndNotCompleted()
    {
        var store = new FakeStore();
        var service = new EmbeddingProfileGovernanceService(store);
        var transition = await service.ProposeTransitionAsync(new EmbeddingProfileTransitionProposal(
            "t", "a", "c", "onnx", "m2", 384, 512, EmbeddingDistanceMetric.Cosine, true, false));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActivateTransitionAsync(transition.TransitionId));
    }

    [Fact]
    public async Task MarkReembeddingCompleted_AllowsActivation()
    {
        var store = new FakeStore();
        var service = new EmbeddingProfileGovernanceService(store);
        var transition = await service.ProposeTransitionAsync(new EmbeddingProfileTransitionProposal(
            "t", "a", "c", "onnx", "m2", 384, 512, EmbeddingDistanceMetric.Cosine, true, false));

        await service.MarkReembeddingCompletedAsync(transition.TransitionId);
        var activated = await service.ActivateTransitionAsync(transition.TransitionId);

        Assert.Equal(EmbeddingProfileTransitionStatus.Activated, activated.Status);
        var current = await store.GetActiveAsync("t", "a", "c");
        Assert.NotNull(current);
        Assert.Equal("m2", current!.Model);
    }

    [Fact]
    public async Task RollbackTransitionAsync_RestoresSourceProfile()
    {
        var store = new FakeStore();
        await store.UpsertActiveAsync(new ActiveEmbeddingProfileRecord("t", "a", "c", "onnx", "m1", 384, 512, EmbeddingDistanceMetric.Cosine, null, null, DateTimeOffset.UtcNow));
        var service = new EmbeddingProfileGovernanceService(store);
        var transition = await service.ProposeTransitionAsync(new EmbeddingProfileTransitionProposal(
            "t", "a", "c", "onnx", "m2", 384, 512, EmbeddingDistanceMetric.Cosine, false, true));

        await service.ActivateTransitionAsync(transition.TransitionId);
        var rollback = await service.RollbackTransitionAsync(transition.TransitionId);

        Assert.Equal(EmbeddingProfileTransitionStatus.RolledBack, rollback.Status);
        var current = await store.GetActiveAsync("t", "a", "c");
        Assert.NotNull(current);
        Assert.Equal("m1", current!.Model);
    }

    private sealed class FakeStore : IActiveEmbeddingProfileStore
    {
        private readonly Dictionary<string, ActiveEmbeddingProfileRecord> _active = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EmbeddingProfileTransitionRecord> _transitions = new(StringComparer.Ordinal);

        public Task<ActiveEmbeddingProfileRecord?> GetActiveAsync(string tenantId, string appId, string collectionId, CancellationToken cancellationToken = default)
        {
            _active.TryGetValue(Key(tenantId, appId, collectionId), out var value);
            return Task.FromResult(value);
        }

        public Task UpsertActiveAsync(ActiveEmbeddingProfileRecord profile, CancellationToken cancellationToken = default)
        {
            _active[Key(profile.TenantId, profile.AppId, profile.CollectionId)] = profile;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ActiveEmbeddingProfileRecord>> GetActivationHistoryAsync(string tenantId, string appId, string collectionId, int take = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ActiveEmbeddingProfileRecord>>([]);

        public Task<EmbeddingProfileCompatibilityResult> CheckCompatibilityAsync(string tenantId, string appId, string collectionId, string provider, string model, int dimensions, CancellationToken cancellationToken = default)
            => Task.FromResult(new EmbeddingProfileCompatibilityResult(true));

        public Task<EmbeddingProfileTransitionRecord> CreateTransitionAsync(EmbeddingProfileTransitionProposal proposal, ActiveEmbeddingProfileRecord? currentProfile, CancellationToken cancellationToken = default)
        {
            var transition = new EmbeddingProfileTransitionRecord(
                Guid.NewGuid().ToString("N"),
                proposal.TenantId,
                proposal.AppId,
                proposal.CollectionId,
                currentProfile?.Provider,
                currentProfile?.Model,
                currentProfile?.Dimensions,
                proposal.TargetProvider,
                proposal.TargetModel,
                proposal.TargetDimensions,
                proposal.TargetMaxTokens,
                proposal.TargetDistanceMetric,
                proposal.TargetVersion,
                proposal.TargetChecksum,
                proposal.RequiresReembedding,
                proposal.ReembeddingCompleted,
                proposal.RequiresReembedding && !proposal.ReembeddingCompleted ? EmbeddingProfileTransitionStatus.Proposed : EmbeddingProfileTransitionStatus.ReadyForActivation,
                proposal.Notes,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            _transitions[transition.TransitionId] = transition;
            return Task.FromResult(transition);
        }

        public Task<EmbeddingProfileTransitionRecord?> GetTransitionByIdAsync(string transitionId, CancellationToken cancellationToken = default)
        {
            _transitions.TryGetValue(transitionId, out var value);
            return Task.FromResult(value);
        }

        public Task<EmbeddingProfileTransitionRecord?> GetLatestPendingTransitionAsync(string tenantId, string appId, string collectionId, CancellationToken cancellationToken = default)
        {
            var value = _transitions.Values
                .Where(t => t.TenantId == tenantId && t.AppId == appId && t.CollectionId == collectionId &&
                            t.Status is EmbeddingProfileTransitionStatus.Proposed or EmbeddingProfileTransitionStatus.ReadyForActivation)
                .OrderByDescending(t => t.CreatedAtUtc)
                .FirstOrDefault();
            return Task.FromResult(value);
        }

        public Task UpdateTransitionAsync(EmbeddingProfileTransitionRecord transition, CancellationToken cancellationToken = default)
        {
            _transitions[transition.TransitionId] = transition;
            return Task.CompletedTask;
        }

        private static string Key(string tenantId, string appId, string collectionId) => $"{tenantId}|{appId}|{collectionId}";
    }
}

