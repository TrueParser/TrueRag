using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Admission;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;
using TrueRag.Workers;

namespace TrueRag.IntegrationTests.Workers;

public sealed class WorkerEmbeddingOrchestrationIntegrationTests
{
    [Fact]
    public async Task ProcessSingleAsync_UsesEmbeddingOrchestrator_WhenIntentRequiresInternalEmbedding()
    {
        var payload = new IngestionRequestDto(
            "doc-1",
            "group-1",
            "1",
            ["g1"],
            "auto",
            [new ChunkDto("n1", null, null, "Paragraph", "text", null, null, [])],
            "collection-1");

        var walReader = new FakeWalReader(payload);
        var repository = new CapturingRepository();
        var orchestrator = new FakeOrchestrator();
        var worker = new IngestionQueueWorker(
            new FakeSubscriber(),
            walReader,
            repository,
            orchestrator,
            new IngestionPressureTracker(),
            new IngestionPressureTracker(),
            Options.Create(new IngestionRuntimeOptions { NodeId = "node-a", WalRootPath = Path.GetTempPath() }),
            Options.Create(new QueueConfiguration { IngestSubjectBase = "TrueRAG.Job.Ingest" }),
            NullLogger<IngestionQueueWorker>.Instance);

        var method = typeof(IngestionQueueWorker).GetMethod("ProcessSingleAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var job = new IngestionJobMessage(
            "node-a",
            "tenant-1",
            "app-1",
            "collection-1",
            "user-1",
            ["reader"],
            ["g1"],
            Path.Combine(Path.GetTempPath(), "worker-test.wal"),
            "seg-1",
            0,
            10,
            true,
            false);

        var task = (Task<bool>)method!.Invoke(worker, [job, CancellationToken.None])!;
        var success = await task;

        Assert.True(success);
        Assert.True(orchestrator.Invoked);
        Assert.NotNull(repository.LastUpsert);
        Assert.Equal(1, repository.LastUpsert!.Chunks.First().Vector.Length);
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

    private sealed class FakeOrchestrator : IIngestionEmbeddingOrchestrator
    {
        public bool Invoked { get; private set; }

        public Task<Result<IngestionRequestDto>> GenerateChunkEmbeddingsIfRequiredAsync(IRequestContext context, IngestionRequestDto payload, IngestionEmbeddingExecutionIntent intent, CancellationToken cancellationToken = default)
        {
            Invoked = true;
            var chunk = payload.Chunks.First();
            var updated = chunk with { Vector = [0.9f] };
            return Task.FromResult(Result<IngestionRequestDto>.Success(payload with { Chunks = [updated], EmbeddingModeTag = "internal_embedding" }));
        }
    }
}
