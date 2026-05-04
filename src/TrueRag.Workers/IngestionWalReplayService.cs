using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;

namespace TrueRag.Workers;

internal sealed class IngestionWalReplayService : BackgroundService
{
    private static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("TRWL");
    private readonly IngestionRuntimeOptions _options;
    private readonly QueueConfiguration _queueOptions;
    private readonly IQueuePublisher _queuePublisher;
    private readonly IWalReadLeaseTracker _leaseTracker;
    private readonly ILogger<IngestionWalReplayService> _logger;

    public IngestionWalReplayService(
        IOptions<IngestionRuntimeOptions> options,
        IOptions<QueueConfiguration> queueOptions,
        IQueuePublisher queuePublisher,
        IWalReadLeaseTracker leaseTracker,
        ILogger<IngestionWalReplayService> logger)
    {
        _options = options.Value;
        _queueOptions = queueOptions.Value;
        _queuePublisher = queuePublisher;
        _leaseTracker = leaseTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Directory.Exists(_options.WalRootPath))
                {
                    foreach (var walPath in Directory.EnumerateFiles(_options.WalRootPath, "*.wal", SearchOption.AllDirectories))
                    {
                        await ReplayFileAsync(walPath, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WAL replay sweep failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ReplayFileAsync(string walPath, CancellationToken cancellationToken)
    {
        var checkpointPath = walPath + ".checkpoint";
        var startOffset = ReadCheckpoint(checkpointPath);
        var topic = $"{_queueOptions.IngestSubjectBase}.{_options.NodeId}";

        using var lease = _leaseTracker.Acquire(walPath);

        await using var fs = new FileStream(walPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.Asynchronous);
        var header = new byte[8];
        var read = await fs.ReadAsync(header, cancellationToken);
        if (read != 8 || !header.AsSpan(0, 4).SequenceEqual(FileMagic))
        {
            return;
        }

        fs.Seek(Math.Max(8, startOffset), SeekOrigin.Begin);

        while (fs.Position < fs.Length)
        {
            var recordStart = fs.Position;
            if (recordStart + 4 > fs.Length)
            {
                break;
            }

            var lengthBytes = new byte[4];
            await fs.ReadExactlyAsync(lengthBytes, cancellationToken);
            var recordLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if (recordLength <= 0 || recordStart + 4 + recordLength > fs.Length)
            {
                break;
            }

            var metadataLengthBytes = new byte[4];
            await fs.ReadExactlyAsync(metadataLengthBytes, cancellationToken);
            var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(metadataLengthBytes);

            var metadataBytes = new byte[metadataLength];
            await fs.ReadExactlyAsync(metadataBytes, cancellationToken);
            var metadata = JsonSerializer.Deserialize<IngestionWalRecordMetadata>(metadataBytes);
            if (metadata is null)
            {
                break;
            }

            var payloadLengthBytes = new byte[8];
            await fs.ReadExactlyAsync(payloadLengthBytes, cancellationToken);
            var payloadLength = BinaryPrimitives.ReadInt64LittleEndian(payloadLengthBytes);
            fs.Seek(payloadLength, SeekOrigin.Current);

            var checksumBytes = new byte[32];
            await fs.ReadExactlyAsync(checksumBytes, cancellationToken);

            var fullLength = recordLength + sizeof(int);
            var markerPath = $"{walPath}.completed.{recordStart}";
            if (!File.Exists(markerPath))
            {
                var message = new IngestionJobMessage(
                    _options.NodeId,
                    metadata.TenantId,
                    metadata.AppId,
                    metadata.CollectionId,
                    "wal-replay",
                    [],
                    [],
                    walPath,
                    Path.GetFileNameWithoutExtension(walPath),
                    recordStart,
                    fullLength);

                await _queuePublisher.PublishAsync(topic, message, cancellationToken);
            }

            var nextOffset = recordStart + fullLength;
            WriteCheckpoint(checkpointPath, nextOffset);
        }
    }

    private static long ReadCheckpoint(string path)
    {
        if (!File.Exists(path))
        {
            return 8;
        }

        var raw = File.ReadAllText(path).Trim();
        return long.TryParse(raw, out var offset) ? offset : 8;
    }

    private static void WriteCheckpoint(string path, long offset)
        => File.WriteAllText(path, offset.ToString());
}
