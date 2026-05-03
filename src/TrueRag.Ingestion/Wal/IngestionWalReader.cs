using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrueRag.Ingestion.Configuration;

namespace TrueRag.Ingestion.Wal;

internal sealed class IngestionWalReader : IIngestionWalReader
{
    private static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("TRWL");
    private readonly string _nodeId;
    private readonly IWalReadLeaseTracker _leaseTracker;

    public IngestionWalReader(IOptions<IngestionRuntimeOptions> options, IWalReadLeaseTracker leaseTracker)
    {
        _nodeId = options.Value.NodeId;
        _leaseTracker = leaseTracker;
    }

    public async Task<Stream> OpenPayloadAsync(
        string nodeId,
        string walPath,
        string walSegmentId,
        long walOffset,
        long walLength,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(nodeId, _nodeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("wal_node_mismatch");
        }

        using var lease = _leaseTracker.Acquire(walPath);

        await using var fs = new FileStream(walPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.Asynchronous);
        var header = new byte[8];
        var read = await fs.ReadAsync(header, cancellationToken);
        if (read != 8 || !header.AsSpan(0, 4).SequenceEqual(FileMagic))
        {
            throw new InvalidDataException("invalid_wal_header");
        }

        fs.Seek(walOffset, SeekOrigin.Begin);

        var lengthBytes = new byte[4];
        await fs.ReadExactlyAsync(lengthBytes, cancellationToken);
        var recordLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);

        var metadataLengthBytes = new byte[4];
        await fs.ReadExactlyAsync(metadataLengthBytes, cancellationToken);
        var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(metadataLengthBytes);

        var metadataBytes = new byte[metadataLength];
        await fs.ReadExactlyAsync(metadataBytes, cancellationToken);
        _ = JsonSerializer.Deserialize<IngestionWalRecordMetadata>(metadataBytes);

        var payloadLenBytes = new byte[8];
        await fs.ReadExactlyAsync(payloadLenBytes, cancellationToken);
        var payloadLength = BinaryPrimitives.ReadInt64LittleEndian(payloadLenBytes);
        var payloadBytes = new byte[payloadLength];
        await fs.ReadExactlyAsync(payloadBytes, cancellationToken);

        var checksumBytes = new byte[32];
        await fs.ReadExactlyAsync(checksumBytes, cancellationToken);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(metadataBytes);
        hash.AppendData(payloadBytes);
        var actual = hash.GetHashAndReset();
        if (!actual.SequenceEqual(checksumBytes))
        {
            throw new InvalidDataException("wal_checksum_mismatch");
        }

        if (walLength != recordLength + sizeof(int))
        {
            throw new InvalidDataException("wal_length_mismatch");
        }

        return new MemoryStream(payloadBytes, writable: false);
    }
}
