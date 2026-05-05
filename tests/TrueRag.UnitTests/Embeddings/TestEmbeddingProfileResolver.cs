using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;

namespace TrueRag.UnitTests.Embeddings;

internal sealed class TestEmbeddingProfileResolver(EmbeddingModelDescriptor descriptor) : IEmbeddingProfileResolver
{
    public Task<EmbeddingModelDescriptor> ResolveActiveDescriptorAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(descriptor);
}
