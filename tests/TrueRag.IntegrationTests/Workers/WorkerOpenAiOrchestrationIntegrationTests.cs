using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Embeddings;
using TrueRag.Ingestion.Admission;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;
using TrueRag.Workers;

namespace TrueRag.IntegrationTests.Workers;

public sealed class WorkerOpenAiOrchestrationIntegrationTests
{
    [Fact]
    public async Task ProcessSingleAsync_OpenAiScopedDescriptor_GeneratesVectorsAndPersists()
    {
        var payload = new IngestionRequestDto(
            "doc-1",
            "group-1",
            "1",
            ["g1"],
            "auto",
            [new ChunkDto("n1", null, null, "Paragraph", "text-a", null, null, []), new ChunkDto("n2", null, null, "Paragraph", "text-b", null, null, [])],
            "collection-1");

        var worker = CreateWorker(
            payload,
            new FakeProfileResolver(new EmbeddingModelDescriptor("openai", "text-embedding-3-small", 3, 8192, EmbeddingDistanceMetric.Cosine)),
            new FakeRegistry(new FakeProvider("openai", shouldFail: false)),
            out var repository);

        var walPath = Path.Combine(Path.GetTempPath(), "worker-openai-ok-" + Guid.NewGuid().ToString("N"));
        var job = CreateJob(walPath);
        var success = await InvokeProcessSingleAsync(worker, job);

        Assert.True(success);
        Assert.NotNull(repository.LastUpsert);
        Assert.All(repository.LastUpsert!.Chunks, chunk => Assert.Equal(3, chunk.Vector.Length));
        Assert.True(File.Exists($"{walPath}.completed.{job.WalOffset}"));
    }

    [Fact]
    public async Task ProcessSingleAsync_OpenAiProviderFailure_DoesNotPersistAndReturnsFalse()
    {
        var payload = new IngestionRequestDto(
            "doc-1",
            "group-1",
            "1",
            ["g1"],
            "auto",
            [new ChunkDto("n1", null, null, "Paragraph", "text-a", null, null, [])],
            "collection-1");

        var worker = CreateWorker(
            payload,
            new FakeProfileResolver(new EmbeddingModelDescriptor("openai", "text-embedding-3-small", 3, 8192, EmbeddingDistanceMetric.Cosine)),
            new FakeRegistry(new FakeProvider("openai", shouldFail: true)),
            out var repository);

        var walPath = Path.Combine(Path.GetTempPath(), "worker-openai-fail-" + Guid.NewGuid().ToString("N"));
        var job = CreateJob(walPath);
        var success = await InvokeProcessSingleAsync(worker, job);

        Assert.False(success);
        Assert.Null(repository.LastUpsert);
        Assert.False(File.Exists($"{walPath}.completed.{job.WalOffset}"));
    }

    private static IngestionQueueWorker CreateWorker(
        IngestionRequestDto payload,
        IEmbeddingProfileResolver resolver,
        IEmbeddingProviderRegistry registry,
        out CapturingRepository repository)
    {
        var walReader = new FakeWalReader(payload);
        repository = new CapturingRepository();
        var orchestrator = new IngestionEmbeddingOrchestrator(resolver, registry, NullLogger<IngestionEmbeddingOrchestrator>.Instance);
        return new IngestionQueueWorker(
            new FakeSubscriber(),
            walReader,
            repository,
            orchestrator,
            new IngestionPressureTracker(),
            new IngestionPressureTracker(),
            Options.Create(new IngestionRuntimeOptions { NodeId = "node-a", WalRootPath = Path.GetTempPath() }),
            Options.Create(new QueueConfiguration { IngestSubjectBase = "TrueRAG.Job.Ingest" }),
            NullLogger<IngestionQueueWorker>.Instance);
    }

    private static IngestionJobMessage CreateJob(string walPath)
        => new(
            "node-a",
            "tenant-1",
            "app-1",
            "collection-1",
            "user-1",
            ["reader"],
            ["g1"],
            walPath,
            "seg-1",
            0,
            10,
            true,
            false);

    private static async Task<bool> InvokeProcessSingleAsync(IngestionQueueWorker worker, IngestionJobMessage job)
    {
        var method = typeof(IngestionQueueWorker).GetMethod("ProcessSingleAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task<bool>)method!.Invoke(worker, [job, CancellationToken.None])!;
        return await task;
    }

    private sealed class FakeSubscriber : IQueueSubscriber
    {
        public Task SubscribeAsync<T>(string topic, string queueGroup, Func<T, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeWalReader(IngestionRequestDto payload) : IIngestionWalReader
    {
        private readonly byte[] _bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        public Task<Stream> OpenPayloadAsync(string nodeId, string walPath, string segmentId, long offset, long length, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(_bytes, writable: false));
    }

    private sealed class CapturingRepository : IIngestionRepository
    {
        public IngestionRequestDto? LastUpsert { get; private set; }

        public Task<Result> UpsertDocumentAsync(IRequestContext requestContext, IngestionRequestDto document, CancellationToken cancellationToken = default)
        {
            LastUpsert = document;
            return Task.FromResult(Result.Success());
        }
    }

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

    private sealed class FakeProvider(string name, bool shouldFail) : IEmbeddingProvider
    {
        public string Name => name;

        public EmbeddingProviderCapabilities Capabilities => new(EmbeddingCapabilityFlags.BatchText | EmbeddingCapabilityFlags.ExternalExecution, 128, [EmbeddingDistanceMetric.Cosine]);

        public Task<EmbedTextResult> EmbedTextAsync(EmbedTextRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EmbedBatchResult> EmbedBatchAsync(EmbedBatchRequest request, CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                throw new InvalidOperationException("status 429 from provider");
            }

            var vectors = request.Inputs.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToArray();
            return Task.FromResult(new EmbedBatchResult(vectors, request.Model, new EmbeddingUsage(request.Inputs.Count)));
        }
    }
}

