using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.Embeddings;

internal sealed class OnnxEmbeddingProfileResolver : IEmbeddingProfileResolver
{
    private readonly IOnnxModelProfileRegistry _profileRegistry;
    private readonly IOptionsMonitor<OnnxProfileSelectionOptions> _selectionOptions;
    private readonly IActiveEmbeddingProfileStore? _profileStore;

    public OnnxEmbeddingProfileResolver(
        IOnnxModelProfileRegistry profileRegistry,
        IOptionsMonitor<OnnxProfileSelectionOptions> selectionOptions,
        IEnumerable<IActiveEmbeddingProfileStore> profileStores)
    {
        _profileRegistry = profileRegistry;
        _selectionOptions = selectionOptions;
        _profileStore = profileStores.FirstOrDefault();
    }

    public async Task<EmbeddingModelDescriptor> ResolveActiveDescriptorAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = _selectionOptions.CurrentValue;
        var scoped = options.ScopedProfiles.FirstOrDefault(profile =>
            string.Equals(profile.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.AppId, appId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.CollectionId, collectionId, StringComparison.OrdinalIgnoreCase));

        var selectedName = scoped?.ProfileName ?? options.DefaultProfileName;
        var profile = _profileRegistry.GetRequiredProfile(selectedName);

        if (options.ExpectedVectorDimensions.HasValue && profile.Dimensions != options.ExpectedVectorDimensions.Value)
        {
            throw new InvalidOperationException(
                $"Embedding profile '{profile.Name}' dimensions ({profile.Dimensions}) do not match expected vector dimensions ({options.ExpectedVectorDimensions.Value}).");
        }

        var descriptor = new EmbeddingModelDescriptor(
            "onnx",
            profile.ModelId,
            profile.Dimensions,
            profile.MaxTokens,
            profile.DistanceMetric);

        if (_profileStore is not null)
        {
            await _profileStore.UpsertActiveAsync(
                new ActiveEmbeddingProfileRecord(
                    tenantId,
                    appId,
                    collectionId,
                    descriptor.Provider,
                    descriptor.Model,
                    descriptor.Dimensions,
                    descriptor.MaxTokens,
                    descriptor.DistanceMetric,
                    descriptor.Version,
                    descriptor.Checksum,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }

        return descriptor;
    }
}
