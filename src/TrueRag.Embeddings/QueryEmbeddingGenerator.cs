using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Embeddings;

internal sealed class QueryEmbeddingGenerator(
    IEmbeddingProfileResolver profileResolver,
    IEmbeddingProviderRegistry providerRegistry) : IQueryEmbeddingGenerator
{
    public async Task<Result<float[]>> GenerateQueryVectorAsync(
        IRequestContext context,
        string queryText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return Result<float[]>.Failure(new Error("retrieval.query_text_required", "QueryText is required for embedding generation.", ErrorType.Validation));
        }

        var descriptor = await profileResolver.ResolveActiveDescriptorAsync(context.TenantId, context.AppId, context.CollectionId, cancellationToken);
        var provider = providerRegistry.GetRequiredProvider(descriptor.Provider);

        var result = await provider.EmbedTextAsync(
            new EmbedTextRequest(
                new EmbeddingInput(queryText),
                descriptor,
                new EmbeddingGenerationContext(context.TenantId, context.AppId, context.CollectionId)),
            cancellationToken);

        return Result<float[]>.Success(result.Vector);
    }
}
