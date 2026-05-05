using Microsoft.Extensions.Logging;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Embeddings;

internal sealed class IngestionEmbeddingOrchestrator(
    IEmbeddingProfileResolver profileResolver,
    IEmbeddingProviderRegistry providerRegistry,
    ILogger<IngestionEmbeddingOrchestrator> logger) : IIngestionEmbeddingOrchestrator
{
    public async Task<Result<IngestionRequestDto>> GenerateChunkEmbeddingsIfRequiredAsync(
        IRequestContext context,
        IngestionRequestDto payload,
        IngestionEmbeddingExecutionIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (!intent.RequiresInternalEmbeddingGeneration)
        {
            return Result<IngestionRequestDto>.Success(payload);
        }

        if (payload.Chunks.Count == 0)
        {
            return Result<IngestionRequestDto>.Failure(new Error(
                "ingestion.embedding_generation_failed",
                "Async ingestion payload does not include chunks.",
                ErrorType.Validation));
        }

        try
        {
            var descriptor = await profileResolver.ResolveActiveDescriptorAsync(
                context.TenantId,
                context.AppId,
                context.CollectionId,
                cancellationToken);

            var provider = providerRegistry.GetRequiredProvider(descriptor.Provider);
            var updated = await GenerateVectorsAsync(provider, descriptor, payload, context, cancellationToken);
            return Result<IngestionRequestDto>.Success(updated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<IngestionRequestDto>.Failure(new Error(
                "ingestion.embedding_generation_cancelled",
                "Embedding generation was cancelled.",
                ErrorType.Validation));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Async embedding generation failed for tenant/app/collection scope.");
            return Result<IngestionRequestDto>.Failure(MapError(ex));
        }
    }

    private static async Task<IngestionRequestDto> GenerateVectorsAsync(
        IEmbeddingProvider provider,
        EmbeddingModelDescriptor descriptor,
        IngestionRequestDto payload,
        IRequestContext context,
        CancellationToken cancellationToken)
    {
        var chunks = payload.Chunks.ToArray();
        var maxBatchSize = provider.Capabilities.Flags.HasFlag(EmbeddingCapabilityFlags.BatchText)
            ? Math.Max(1, provider.Capabilities.MaxBatchSize)
            : 1;
        var generatedVectors = new List<float[]>(chunks.Length);

        for (var offset = 0; offset < chunks.Length; offset += maxBatchSize)
        {
            var batch = chunks.Skip(offset).Take(maxBatchSize).ToArray();
            var inputs = batch.Select(static chunk => new EmbeddingInput(chunk.Text)).ToArray();
            var generationContext = new EmbeddingGenerationContext(context.TenantId, context.AppId, context.CollectionId);

            if (provider.Capabilities.Flags.HasFlag(EmbeddingCapabilityFlags.BatchText))
            {
                var batchResult = await provider.EmbedBatchAsync(
                    new EmbedBatchRequest(inputs, descriptor, generationContext),
                    cancellationToken);
                generatedVectors.AddRange(batchResult.Vectors);
            }
            else
            {
                foreach (var input in inputs)
                {
                    var singleResult = await provider.EmbedTextAsync(
                        new EmbedTextRequest(input, descriptor, generationContext),
                        cancellationToken);
                    generatedVectors.Add(singleResult.Vector);
                }
            }
        }

        if (generatedVectors.Count != chunks.Length)
        {
            throw new InvalidOperationException("Embedding provider returned vector count that does not match chunk count.");
        }

        if (generatedVectors.Any(vector => vector.Length != descriptor.Dimensions))
        {
            throw new InvalidOperationException(
                $"Embedding provider returned vectors incompatible with descriptor dimensions ({descriptor.Dimensions}).");
        }

        var rewrittenChunks = chunks
            .Select((chunk, index) => chunk with { Vector = generatedVectors[index] })
            .ToArray();

        return payload with
        {
            Chunks = rewrittenChunks,
            EmbeddingModeTag = "internal_embedding"
        };
    }

    private static Error MapError(Exception exception)
    {
        var text = exception.ToString();

        if (ContainsAny(text, " 401", "unauthorized", "forbidden"))
        {
            return new Error(
                "ingestion.embedding_provider_auth_failed",
                "Embedding provider authentication failed.",
                ErrorType.Unavailable);
        }

        if (ContainsAny(text, " 429", "rate limit", "too many requests"))
        {
            return new Error(
                "ingestion.embedding_provider_rate_limited",
                "Embedding provider rate limit exceeded.",
                ErrorType.Unavailable);
        }

        if (exception is TimeoutException || ContainsAny(text, "timed out", "timeout", "TaskCanceledException"))
        {
            return new Error(
                "ingestion.embedding_provider_timeout",
                "Embedding provider request timed out.",
                ErrorType.Unavailable);
        }

        return new Error(
            "ingestion.embedding_provider_unavailable",
            "Embedding provider is unavailable.",
            ErrorType.Unavailable);
    }

    private static bool ContainsAny(string input, params string[] values)
        => values.Any(value => input.Contains(value, StringComparison.OrdinalIgnoreCase));
}
