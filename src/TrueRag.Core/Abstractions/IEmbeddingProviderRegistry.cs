using TrueRag.Core.Models;

namespace TrueRag.Core.Abstractions;

public interface IEmbeddingProviderRegistry
{
    IEmbeddingProvider GetRequiredProvider(string provider);

    bool TryGetProvider(string provider, out IEmbeddingProvider? embeddingProvider);

    IReadOnlyCollection<string> GetRegisteredProviderNames();
}

public interface IEmbeddingProfileResolver
{
    Task<EmbeddingModelDescriptor> ResolveActiveDescriptorAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default);
}
