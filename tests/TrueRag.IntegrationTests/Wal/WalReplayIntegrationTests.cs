using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;
using TrueRag.Workers;

namespace TrueRag.IntegrationTests.Wal;

public sealed class WalReplayIntegrationTests
{
    [Fact]
    public async Task ReplayFileAsync_PublishesPendingRecord_AndWritesCheckpoint()
    {
        var root = Path.Combine(Path.GetTempPath(), "truerag-int-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var walPath = Path.Combine(root, "lane-0.wal");
            WriteWalWithSingleRecord(walPath);

            var publisher = new CapturingPublisher();
            var options = Options.Create(new IngestionRuntimeOptions
            {
                NodeId = "node-a",
                WalRootPath = root
            });
            var queueOptions = Options.Create(new QueueConfiguration
            {
                IngestSubjectBase = "TrueRAG.Job.Ingest"
            });

            var service = new IngestionWalReplayService(
                options,
                queueOptions,
                publisher,
                new TestLeaseTracker(),
                NullLogger<IngestionWalReplayService>.Instance);

            var replayMethod = typeof(IngestionWalReplayService).GetMethod(
                "ReplayFileAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(replayMethod);

            var task = (Task)replayMethod!.Invoke(service, [walPath, CancellationToken.None])!;
            await task;

            Assert.Single(publisher.Messages);
            Assert.Equal("TrueRAG.Job.Ingest.node-a", publisher.Messages[0].topic);
            Assert.Equal(walPath, publisher.Messages[0].message.WalPath);
            Assert.True(File.Exists(walPath + ".checkpoint"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteWalWithSingleRecord(string walPath)
    {
        var metadata = new IngestionWalRecordMetadata("tenant-1", "app-1", "collection-1", "doc-1", "corr-1", "node-a");
        var metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata);
        var payloadBytes = Encoding.UTF8.GetBytes("{\"documentId\":\"doc-1\"}");
        var checksumBytes = new byte[32];

        var recordLength = sizeof(int) + metadataBytes.Length + sizeof(long) + payloadBytes.Length + checksumBytes.Length;
        var recordBody = new byte[recordLength];

        var offset = 0;
        BinaryPrimitives.WriteInt32LittleEndian(recordBody.AsSpan(offset, sizeof(int)), metadataBytes.Length);
        offset += sizeof(int);

        metadataBytes.CopyTo(recordBody.AsSpan(offset, metadataBytes.Length));
        offset += metadataBytes.Length;

        BinaryPrimitives.WriteInt64LittleEndian(recordBody.AsSpan(offset, sizeof(long)), payloadBytes.Length);
        offset += sizeof(long);

        payloadBytes.CopyTo(recordBody.AsSpan(offset, payloadBytes.Length));
        offset += payloadBytes.Length;

        checksumBytes.CopyTo(recordBody.AsSpan(offset, checksumBytes.Length));

        using var fs = new FileStream(walPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
        fs.Write(Encoding.ASCII.GetBytes("TRWL"));
        fs.Write(stackalloc byte[4]);

        Span<byte> recordLengthPrefix = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(recordLengthPrefix, recordLength);
        fs.Write(recordLengthPrefix);
        fs.Write(recordBody);
    }

    private sealed class CapturingPublisher : IQueuePublisher
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

    private sealed class TestLeaseTracker : IWalReadLeaseTracker
    {
        public IDisposable Acquire(string walFilePath) => new Lease();

        public bool IsLeased(string walPath) => false;

        private sealed class Lease : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
