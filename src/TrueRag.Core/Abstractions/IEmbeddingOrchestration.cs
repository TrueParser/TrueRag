using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public sealed record IngestionEmbeddingExecutionIntent(
    bool RequiresInternalEmbeddingGeneration,
    bool UsesPrecomputedVectors,
    string WalPath,
    string WalSegmentId,
    long WalOffset,
    long WalLength);

public interface IIngestionEmbeddingOrchestrator
{
    Task<Result<IngestionRequestDto>> GenerateChunkEmbeddingsIfRequiredAsync(
        IRequestContext context,
        IngestionRequestDto payload,
        IngestionEmbeddingExecutionIntent intent,
        CancellationToken cancellationToken = default);
}

public interface IQueryEmbeddingGenerator
{
    Task<Result<float[]>> GenerateQueryVectorAsync(
        IRequestContext context,
        string queryText,
        CancellationToken cancellationToken = default);
}
