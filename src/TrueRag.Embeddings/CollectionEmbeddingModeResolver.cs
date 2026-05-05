using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.Embeddings;

internal sealed class CollectionEmbeddingModeResolver(IOptionsMonitor<EmbeddingModeSelectionOptions> options) : ICollectionEmbeddingModeResolver
{
    public Task<CollectionEmbeddingMode> ResolveModeAsync(
        IRequestContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = options.CurrentValue;

        var scoped = current.ScopedModes.FirstOrDefault(mode =>
            string.Equals(mode.TenantId, context.TenantId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(mode.AppId, context.AppId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(mode.CollectionId, context.CollectionId, StringComparison.OrdinalIgnoreCase));

        var value = scoped?.Mode ?? current.DefaultMode;
        return Task.FromResult(Parse(value));
    }

    private static CollectionEmbeddingMode Parse(string value)
        => string.Equals(value, "external_embedding", StringComparison.OrdinalIgnoreCase)
            ? CollectionEmbeddingMode.ExternalEmbedding
            : CollectionEmbeddingMode.InternalEmbedding;
}
