using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Core.Models;
using TrueRag.Embeddings;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OnnxEmbeddingWarmupServiceTests
{
    [Fact]
    public async Task StartAsync_Throws_WhenArtifactsInvalid()
    {
        var options = Options.Create(new OnnxEmbeddingOptions
        {
            ModelArtifactsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            ModelFileName = "model.onnx",
            EnableWarmup = true
        });

        var descriptor = new EmbeddingModelDescriptor("onnx", "BAAI/bge-small-en-v1.5", 384, 512, EmbeddingDistanceMetric.Cosine);

        var provider = new OnnxEmbeddingProvider(
            new StaticOptionsMonitor<OnnxEmbeddingOptions>(options.Value),
            new StaticOptionsMonitor<OnnxProfileSelectionOptions>(OnnxTestProfiles.CreateSelectionOptions()),
            new TestEmbeddingProfileResolver(descriptor),
            new OnnxModelProfileRegistry(),
            new OnnxTextPreprocessor(),
            new OnnxModelArtifactValidator(),
            new DeterministicOnnxEmbeddingExecutor(),
            NullLogger<OnnxEmbeddingProvider>.Instance);

        var service = new OnnxEmbeddingWarmupService(provider, options, NullLogger<OnnxEmbeddingWarmupService>.Instance);

        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_Completes_WhenArtifactsValid()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "model.onnx"), [1, 2, 3]);

        try
        {
            var options = Options.Create(new OnnxEmbeddingOptions
            {
                ModelArtifactsPath = directory,
                ModelFileName = "model.onnx",
                EnableWarmup = true,
                WarmupTimeoutSeconds = 3
            });

            var descriptor = new EmbeddingModelDescriptor("onnx", "BAAI/bge-small-en-v1.5", 384, 512, EmbeddingDistanceMetric.Cosine);
            var provider = new OnnxEmbeddingProvider(
                new StaticOptionsMonitor<OnnxEmbeddingOptions>(options.Value),
                new StaticOptionsMonitor<OnnxProfileSelectionOptions>(OnnxTestProfiles.CreateSelectionOptions()),
                new TestEmbeddingProfileResolver(descriptor),
                new OnnxModelProfileRegistry(),
                new OnnxTextPreprocessor(),
                new OnnxModelArtifactValidator(),
                new DeterministicOnnxEmbeddingExecutor(),
                NullLogger<OnnxEmbeddingProvider>.Instance);

            var service = new OnnxEmbeddingWarmupService(provider, options, NullLogger<OnnxEmbeddingWarmupService>.Instance);
            await service.StartAsync(CancellationToken.None);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
