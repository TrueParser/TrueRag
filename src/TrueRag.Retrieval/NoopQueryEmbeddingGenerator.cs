using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Primitives;

namespace TrueRag.Retrieval;

internal sealed class NoopQueryEmbeddingGenerator : IQueryEmbeddingGenerator
{
    public Task<Result<float[]>> GenerateQueryVectorAsync(
        IRequestContext context,
        string queryText,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<float[]>.Failure(new Error(
            "retrieval.query_embedding_generation_not_configured",
            "Query embedding generation is not configured.",
            ErrorType.Validation)));
}
