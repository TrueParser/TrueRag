using Microsoft.Extensions.Logging.Abstractions;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Embeddings;

namespace TrueRag.UnitTests.Embeddings;

public sealed class IngestionEmbeddingOrchestratorTests
{
    [Fact]
    public async Task GenerateChunkEmbeddingsIfRequiredAsync_UsesDescriptorProviderAndPopulatesVectors()
    {
        var provider = new FakeProvider("openai", batch: true);
        var registry = new FakeRegistry(provider);
        var resolver = new FakeProfileResolver(new EmbeddingModelDescriptor("openai", "text-embedding-3-small", 3, 8192, EmbeddingDistanceMetric.Cosine));
        var orchestrator = new IngestionEmbeddingOrchestrator(resolver, registry, NullLogger<IngestionEmbeddingOrchestrator>.Instance);

        var payload = CreatePayload();
        var context = new RequestContext("t", "a", "u", ["writer"], ["g"], "c");
        var result = await orchestrator.GenerateChunkEmbeddingsIfRequiredAsync(
            context,
            payload,
            new IngestionEmbeddingExecutionIntent(true, false, "wal", "seg", 0, 1));

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Chunks, chunk => Assert.Equal(3, chunk.Vector.Length));
    }

    [Fact]
    public async Task GenerateChunkEmbeddingsIfRequiredAsync_WhenProviderRateLimited_ReturnsDeterministicError()
    {
        var provider = new ThrowingProvider("openai", new InvalidOperationException("status 429 from provider"));
        var registry = new FakeRegistry(provider);
        var resolver = new FakeProfileResolver(new EmbeddingModelDescriptor("openai", "text-embedding-3-small", 3, 8192, EmbeddingDistanceMetric.Cosine));
        var orchestrator = new IngestionEmbeddingOrchestrator(resolver, registry, NullLogger<IngestionEmbeddingOrchestrator>.Instance);

        var payload = CreatePayload();
        var context = new RequestContext("t", "a", "u", ["writer"], ["g"], "c");
        var result = await orchestrator.GenerateChunkEmbeddingsIfRequiredAsync(
            context,
            payload,
            new IngestionEmbeddingExecutionIntent(true, false, "wal", "seg", 0, 1));

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.embedding_provider_rate_limited", result.Error?.Code);
    }

    private static IngestionRequestDto CreatePayload()
        => new(
            "doc",
            "group",
            "1",
            ["g"],
            "auto",
            [
                new ChunkDto("n1", null, null, "Paragraph", "hello", null, null, []),
                new ChunkDto("n2", null, null, "Paragraph", "world", null, null, [])
            ],
            "c");

    private sealed class FakeProfileResolver(EmbeddingModelDescriptor descriptor) : IEmbeddingProfileResolver
    {
        public Task<EmbeddingModelDescriptor> ResolveActiveDescriptorAsync(string tenantId, string appId, string collectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(descriptor);
    }

    private sealed class FakeRegistry(IEmbeddingProvider provider) : IEmbeddingProviderRegistry
    {
        public IEmbeddingProvider GetRequiredProvider(string providerName) => provider;

        public bool TryGetProvider(string providerName, out IEmbeddingProvider? embeddingProvider)
        {
            embeddingProvider = provider;
            return true;
        }

        public IReadOnlyCollection<string> GetRegisteredProviderNames() => [provider.Name];
    }

    private sealed class FakeProvider(string name, bool batch) : IEmbeddingProvider
    {
        public string Name => name;

        public EmbeddingProviderCapabilities Capabilities => batch
            ? new EmbeddingProviderCapabilities(EmbeddingCapabilityFlags.BatchText | EmbeddingCapabilityFlags.ExternalExecution, 64, [EmbeddingDistanceMetric.Cosine])
            : new EmbeddingProviderCapabilities(EmbeddingCapabilityFlags.SingleText | EmbeddingCapabilityFlags.ExternalExecution, 1, [EmbeddingDistanceMetric.Cosine]);

        public Task<EmbedTextResult> EmbedTextAsync(EmbedTextRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new EmbedTextResult([0.1f, 0.2f, 0.3f], request.Model, new EmbeddingUsage(1)));

        public Task<EmbedBatchResult> EmbedBatchAsync(EmbedBatchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new EmbedBatchResult(request.Inputs.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToArray(), request.Model, new EmbeddingUsage(request.Inputs.Count)));
    }

    private sealed class ThrowingProvider(string name, Exception exception) : IEmbeddingProvider
    {
        public string Name => name;

        public EmbeddingProviderCapabilities Capabilities => new(EmbeddingCapabilityFlags.BatchText | EmbeddingCapabilityFlags.ExternalExecution, 64, [EmbeddingDistanceMetric.Cosine]);

        public Task<EmbedTextResult> EmbedTextAsync(EmbedTextRequest request, CancellationToken cancellationToken = default)
            => throw exception;

        public Task<EmbedBatchResult> EmbedBatchAsync(EmbedBatchRequest request, CancellationToken cancellationToken = default)
            => throw exception;
    }
}

