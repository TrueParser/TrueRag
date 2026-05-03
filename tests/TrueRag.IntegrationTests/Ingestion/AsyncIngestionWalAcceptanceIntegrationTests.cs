using System.Text.Json;
using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Adapters;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Execution;
using TrueRag.Ingestion.Normalization;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;

namespace TrueRag.IntegrationTests.Ingestion;

public sealed class AsyncIngestionWalAcceptanceIntegrationTests
{
    [Fact]
    public async Task IngestAsyncBuffered_WritesWalAndPublishesCoordinates()
    {
        var walRoot = Path.Combine(Path.GetTempPath(), "truerag-wal-int-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(walRoot);

        try
        {
            var runtimeOptions = Options.Create(new IngestionRuntimeOptions
            {
                NodeId = "node-int-a",
                WalRootPath = walRoot,
                WalFsync = false,
                WalMaxSegmentBytes = 4 * 1024 * 1024
            });
            var queueOptions = Options.Create(new QueueConfiguration
            {
                IngestSubjectBase = "TrueRAG.Job.Ingest"
            });

            var fidelityOptions = Options.Create(new IngestionFidelityOptions
            {
                DefaultMode = "auto",
                AllowExplicitOverride = true
            });

            var adapter = new CanonicalIngestionPayloadAdapter(fidelityOptions);
            var normalizer = new IngestionNormalizer(adapter);
            var acceptanceLog = new IngestionAcceptanceLog(runtimeOptions);
            var publisher = new CapturingQueuePublisher();
            var repository = new NoOpIngestionRepository();

            var service = new IngestionExecutionService(
                normalizer,
                acceptanceLog,
                publisher,
                repository,
                runtimeOptions,
                queueOptions);

            var context = new RequestContext("tenant-a", "app-a", "user-a", ["writer"], ["legal"]);
            var payload = new IngestionRequestDto(
                DocumentId: "doc-async-1",
                DocumentGroupId: "group-async",
                VersionNumber: "1.0",
                AllowedDocumentGroups: ["legal"],
                Fidelity: "auto",
                Chunks:
                [
                    new ChunkDto(
                        Id: "node-1",
                        ParentId: "section-1",
                        LogicalPath: "Document/Section1/Paragraph1",
                        Type: "Paragraph",
                        Text: "Async ingestion payload",
                        BoundingBox: new BoundingBoxDto(1, 10, 20, 30, 40),
                        ReferencedNodeIds: ["node-2"],
                        Vector: [0.4f, 0.5f, 0.6f])
                ]);

            var result = await service.IngestAsyncBuffered(context, payload);

            Assert.True(result.IsSuccess);
            Assert.Single(publisher.Messages);

            var (topic, message) = publisher.Messages[0];
            Assert.Equal("TrueRAG.Job.Ingest.node-int-a", topic);
            Assert.Equal("node-int-a", message.NodeId);
            Assert.True(File.Exists(message.WalPath));
            Assert.True(message.WalLength > 0);

            var reader = new IngestionWalReader(runtimeOptions, new WalReadLeaseTracker());
            await using var payloadStream = await reader.OpenPayloadAsync(
                message.NodeId,
                message.WalPath,
                message.WalSegmentId,
                message.WalOffset,
                message.WalLength);

            var restored = await JsonSerializer.DeserializeAsync<IngestionRequestDto>(payloadStream);
            Assert.NotNull(restored);
            Assert.Equal("doc-async-1", restored!.DocumentId);
            Assert.Equal("high", restored.Fidelity);
        }
        finally
        {
            Directory.Delete(walRoot, recursive: true);
        }
    }

    private sealed class CapturingQueuePublisher : IQueuePublisher
    {
        public List<(string topic, IngestionJobMessage message)> Messages { get; } = [];

        public Task PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default)
        {
            if (message is IngestionJobMessage job)
            {
                Messages.Add((topic, job));
            }

            return Task.CompletedTask;
        }
    }

    private sealed class NoOpIngestionRepository : IIngestionRepository
    {
        public Task<Result> UpsertDocumentAsync(IRequestContext requestContext, IngestionRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }
}
