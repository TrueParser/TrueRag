using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrueRag.Ingestion.Configuration;

namespace TrueRag.Ingestion.Wal;

internal sealed class IngestionAcceptanceLog : IIngestionAcceptanceLog
{
    private static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("TRWL");
    private readonly string _walRoot;
    private readonly bool _walFsync;
    private readonly long _walMaxSegmentBytes;
    private readonly SemaphoreSlim _appendGate = new(1, 1);

    public IngestionAcceptanceLog(IOptions<IngestionRuntimeOptions> options)
    {
        _walRoot = options.Value.WalRootPath;
        _walFsync = options.Value.WalFsync;
        _walMaxSegmentBytes = Math.Max(1024 * 1024, options.Value.WalMaxSegmentBytes);
    }

    public async Task<IngestionWalAppendResult> AppendAsync(
        IngestionWalRecordMetadata metadata,
        Stream payload,
        long payloadLength,
        CancellationToken cancellationToken = default)
    {
        var shardIndex = ResolveShardIndex(metadata.TenantId, metadata.AppId, metadata.DocumentId);
        var laneKey = $"{metadata.TenantId}:{metadata.AppId}:shard-{shardIndex}";
        var laneDir = Path.Combine(_walRoot, Sanitize(metadata.TenantId), Sanitize(metadata.AppId), $"shard-{shardIndex:D2}");
        Directory.CreateDirectory(laneDir);

        var metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata);
        using var ms = new MemoryStream();
        await payload.CopyToAsync(ms, cancellationToken);
        var payloadBytes = ms.ToArray();
        if (payloadBytes.LongLength != payloadLength)
        {
            payloadLength = payloadBytes.LongLength;
        }

        var lengthBuffer = new byte[4];
        var metadataLenBuffer = new byte[4];
        var payloadLenBuffer = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(metadataLenBuffer, metadataBytes.Length);
        BinaryPrimitives.WriteInt64LittleEndian(payloadLenBuffer, payloadLength);

        using var crc = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        crc.AppendData(metadataBytes);
        crc.AppendData(payloadBytes);
        var checksum = crc.GetHashAndReset();

        var recordLength = sizeof(int) + metadataBytes.Length + sizeof(long) + payloadBytes.Length + checksum.Length;
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, recordLength);
        var fullRecordLength = recordLength + sizeof(int);

        await _appendGate.WaitAsync(cancellationToken);
        try
        {
            var (segmentId, walPath) = ResolveActiveSegment(laneDir);

            await using var fs = new FileStream(walPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 64 * 1024, FileOptions.Asynchronous);
            if (fs.Length == 0)
            {
                await fs.WriteAsync(FileMagic, cancellationToken);
                var version = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(version, 1);
                await fs.WriteAsync(version, cancellationToken);
            }

            if (fs.Length + fullRecordLength > _walMaxSegmentBytes)
            {
                segmentId = NextSegmentId(segmentId);
                walPath = Path.Combine(laneDir, segmentId + ".wal");
                WriteActiveSegmentId(laneDir, segmentId);

                await fs.DisposeAsync();
                await using var rotated = new FileStream(walPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 64 * 1024, FileOptions.Asynchronous);
                if (rotated.Length == 0)
                {
                    await rotated.WriteAsync(FileMagic, cancellationToken);
                    var version = new byte[4];
                    BinaryPrimitives.WriteInt32LittleEndian(version, 1);
                    await rotated.WriteAsync(version, cancellationToken);
                }

                rotated.Seek(0, SeekOrigin.End);
                var rotatedOffset = rotated.Position;
                await rotated.WriteAsync(lengthBuffer, cancellationToken);
                await rotated.WriteAsync(metadataLenBuffer, cancellationToken);
                await rotated.WriteAsync(metadataBytes, cancellationToken);
                await rotated.WriteAsync(payloadLenBuffer, cancellationToken);
                await rotated.WriteAsync(payloadBytes, cancellationToken);
                await rotated.WriteAsync(checksum, cancellationToken);
                await rotated.FlushAsync(cancellationToken);
                if (_walFsync)
                {
                    rotated.Flush(flushToDisk: true);
                }

                return new IngestionWalAppendResult(
                    metadata.TenantId,
                    metadata.AppId,
                    laneKey,
                    walPath,
                    segmentId,
                    rotatedOffset,
                    fullRecordLength,
                    shardIndex);
            }

            fs.Seek(0, SeekOrigin.End);
            var offset = fs.Position;

            await fs.WriteAsync(lengthBuffer, cancellationToken);
            await fs.WriteAsync(metadataLenBuffer, cancellationToken);
            await fs.WriteAsync(metadataBytes, cancellationToken);
            await fs.WriteAsync(payloadLenBuffer, cancellationToken);
            await fs.WriteAsync(payloadBytes, cancellationToken);
            await fs.WriteAsync(checksum, cancellationToken);
            await fs.FlushAsync(cancellationToken);

            if (_walFsync)
            {
                fs.Flush(flushToDisk: true);
            }

            return new IngestionWalAppendResult(
                metadata.TenantId,
                metadata.AppId,
                laneKey,
                walPath,
                segmentId,
                offset,
                fullRecordLength,
                shardIndex);
        }
        finally
        {
            _appendGate.Release();
        }
    }

    public IEnumerable<string> EnumerateLaneFiles()
    {
        if (!Directory.Exists(_walRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(_walRoot, "*.wal", SearchOption.AllDirectories);
    }

    private static (string SegmentId, string WalPath) ResolveActiveSegment(string laneDir)
    {
        var activeFile = Path.Combine(laneDir, ".active");
        if (File.Exists(activeFile))
        {
            var segmentId = File.ReadAllText(activeFile).Trim();
            if (!string.IsNullOrWhiteSpace(segmentId))
            {
                return (segmentId, Path.Combine(laneDir, segmentId + ".wal"));
            }
        }

        var defaultSegmentId = "segment-00000000000000000000";
        WriteActiveSegmentId(laneDir, defaultSegmentId);
        return (defaultSegmentId, Path.Combine(laneDir, defaultSegmentId + ".wal"));
    }

    private static void WriteActiveSegmentId(string laneDir, string segmentId)
        => File.WriteAllText(Path.Combine(laneDir, ".active"), segmentId);

    private static string NextSegmentId(string currentSegmentId)
    {
        var suffix = currentSegmentId.Replace("segment-", string.Empty, StringComparison.Ordinal);
        if (!long.TryParse(suffix, out var value))
        {
            value = 0;
        }

        return $"segment-{value + 1:D20}";
    }

    private static int ResolveShardIndex(string tenantId, string appId, string documentId)
    {
        var input = $"{tenantId}:{appId}:{documentId}";
        var hash = input.GetHashCode(StringComparison.Ordinal);
        return Math.Abs(hash % 8);
    }

    private static string Sanitize(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "_";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            segment = segment.Replace(invalid, '_');
        }

        return segment;
    }
}
