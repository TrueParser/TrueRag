using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;

namespace TrueRag.Retrieval;

internal sealed class NoopCollectionEmbeddingModeResolver : ICollectionEmbeddingModeResolver
{
    public Task<CollectionEmbeddingMode> ResolveModeAsync(IRequestContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(CollectionEmbeddingMode.ExternalEmbedding);
}
