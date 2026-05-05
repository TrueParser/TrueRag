using TrueRag.Core.Context;

namespace TrueRag.Core.Abstractions;

public enum CollectionEmbeddingMode
{
    InternalEmbedding = 0,
    ExternalEmbedding = 1
}

public interface ICollectionEmbeddingModeResolver
{
    Task<CollectionEmbeddingMode> ResolveModeAsync(
        IRequestContext context,
        CancellationToken cancellationToken = default);
}
