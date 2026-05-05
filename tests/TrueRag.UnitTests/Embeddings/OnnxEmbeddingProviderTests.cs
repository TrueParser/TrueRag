using Microsoft.Extensions.Logging.Abstractions;
using TrueRag.Core.Models;
using TrueRag.Embeddings;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OnnxEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedBatchAsync_RespectsCancellation()
    {
        var directory = CreateModelDirectory();

        try
        {
            var provider = CreateProvider(directory);
            var descriptor = new EmbeddingModelDescriptor("onnx", "BAAI/bge-small-en-v1.5", 384, 512, EmbeddingDistanceMetric.Cosine);
            var request = new EmbedBatchRequest([new EmbeddingInput("alpha"), new EmbeddingInput("beta")], descriptor);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => provider.EmbedBatchAsync(request, cts.Token));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task EmbedTextAsync_ThrowsTimeout_WhenExecutorExceedsConfiguredTimeout()
    {
        var directory = CreateModelDirectory();

        try
        {
            var provider = CreateProvider(directory, new DelayedExecutor(TimeSpan.FromSeconds(2)), inferenceTimeoutSeconds: 1);
            var descriptor = new EmbeddingModelDescriptor("onnx", "BAAI/bge-small-en-v1.5", 384, 512, EmbeddingDistanceMetric.Cosine);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.EmbedTextAsync(new EmbedTextRequest(new EmbeddingInput("hello world"), descriptor)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task EmbedTextAsync_ReturnsConfiguredDimension()
    {
        var directory = CreateModelDirectory();

        try
        {
            var provider = CreateProvider(directory);
            var descriptor = new EmbeddingModelDescriptor("onnx", "BAAI/bge-small-en-v1.5", 384, 512, EmbeddingDistanceMetric.Cosine);

            var result = await provider.EmbedTextAsync(new EmbedTextRequest(new EmbeddingInput("hello world"), descriptor));

            Assert.Equal(384, result.Vector.Length);
            Assert.Equal("BAAI/bge-small-en-v1.5", result.Model.Model);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static OnnxEmbeddingProvider CreateProvider(
        string modelDirectory,
        IOnnxEmbeddingExecutor? executor = null,
        int inferenceTimeoutSeconds = 15)
    {
        var options = new OnnxEmbeddingOptions
        {
            ModelArtifactsPath = modelDirectory,
            ModelFileName = "model.onnx",
            Dimensions = 384,
            MaxTokens = 512,
            MaxBatchSize = 8,
            ProviderName = "onnx",
            ModelId = "BAAI/bge-small-en-v1.5",
            InferenceTimeoutSeconds = inferenceTimeoutSeconds
        };

        var descriptor = new EmbeddingModelDescriptor("onnx", "BAAI/bge-small-en-v1.5", 384, 512, EmbeddingDistanceMetric.Cosine);

        return new OnnxEmbeddingProvider(
            new StaticOptionsMonitor<OnnxEmbeddingOptions>(options),
            new StaticOptionsMonitor<OnnxProfileSelectionOptions>(OnnxTestProfiles.CreateSelectionOptions()),
            new TestEmbeddingProfileResolver(descriptor),
            new OnnxModelProfileRegistry(),
            new OnnxTextPreprocessor(),
            new OnnxModelArtifactValidator(),
            executor ?? new DeterministicOnnxEmbeddingExecutor(),
            NullLogger<OnnxEmbeddingProvider>.Instance);
    }

    private static string CreateModelDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "model.onnx"), [1, 2, 3]);
        return directory;
    }

    private sealed class DelayedExecutor(TimeSpan delay) : IOnnxEmbeddingExecutor
    {
        public async Task<float[]> GenerateVectorAsync(string normalizedText, int dimensions, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return new float[dimensions];
        }
    }
}
