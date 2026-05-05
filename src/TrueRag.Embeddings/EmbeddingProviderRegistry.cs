using TrueRag.Core.Abstractions;

namespace TrueRag.Embeddings;

internal sealed class EmbeddingProviderRegistry : IEmbeddingProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IEmbeddingProvider> _providers;

    public EmbeddingProviderRegistry(IEnumerable<IEmbeddingProvider> providers)
    {
        _providers = providers.ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEmbeddingProvider GetRequiredProvider(string provider)
    {
        if (TryGetProvider(provider, out var embeddingProvider) && embeddingProvider is not null)
        {
            return embeddingProvider;
        }

        throw new InvalidOperationException($"Embedding provider '{provider}' is not registered.");
    }

    public bool TryGetProvider(string provider, out IEmbeddingProvider? embeddingProvider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            embeddingProvider = null;
            return false;
        }

        return _providers.TryGetValue(provider, out embeddingProvider);
    }

    public IReadOnlyCollection<string> GetRegisteredProviderNames() => _providers.Keys.ToArray();
}
