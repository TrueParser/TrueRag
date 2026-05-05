using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Queue;

namespace TrueRag.Workers;

internal sealed class NoopIngestionEmbeddingOrchestrator : IIngestionEmbeddingOrchestrator
{
    public Task<Result<IngestionRequestDto>> GenerateChunkEmbeddingsIfRequiredAsync(
        IRequestContext context,
        IngestionRequestDto payload,
        IngestionEmbeddingExecutionIntent intent,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<IngestionRequestDto>.Failure(new Error(
            "ingestion.embedding_generation_not_configured",
            "Internal embedding generation is not configured for worker execution.",
            ErrorType.Unavailable)));
}
