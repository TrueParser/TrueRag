using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;
using TrueRag.Embeddings;

namespace TrueRag.UnitTests.Embeddings;

public sealed class EmbeddingsModuleRegistrationTests
{
    [Fact]
    public void AddTrueRagEmbeddings_RegistersProviderRegistry()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmbeddingProvider, FakeEmbeddingProvider>();
        services.AddTrueRagEmbeddings();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IEmbeddingProviderRegistry>();

        var resolved = registry.GetRequiredProvider("fake");

        Assert.NotNull(resolved);
        Assert.Equal("fake", resolved.Name);
    }

    private sealed class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public string Name => "fake";

        public EmbeddingProviderCapabilities Capabilities { get; } = new(
            EmbeddingCapabilityFlags.SingleText | EmbeddingCapabilityFlags.BatchText,
            32,
            [EmbeddingDistanceMetric.Cosine]);

        public Task<EmbedTextResult> EmbedTextAsync(EmbedTextRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmbedTextResult([0.1f, 0.2f], request.Model, new EmbeddingUsage(1)));
        }

        public Task<EmbedBatchResult> EmbedBatchAsync(EmbedBatchRequest request, CancellationToken cancellationToken = default)
        {
            var vectors = request.Inputs.Select(_ => new[] { 0.1f, 0.2f } as float[]).ToArray();
            return Task.FromResult(new EmbedBatchResult(vectors, request.Model, new EmbeddingUsage(request.Inputs.Count)));
        }
    }
}
