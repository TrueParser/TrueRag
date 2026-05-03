namespace TrueRag.Ingestion.Wal;

public interface IIngestionWalReader
{
    Task<Stream> OpenPayloadAsync(
        string nodeId,
        string walPath,
        string walSegmentId,
        long walOffset,
        long walLength,
        CancellationToken cancellationToken = default);
}