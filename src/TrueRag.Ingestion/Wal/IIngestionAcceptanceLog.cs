namespace TrueRag.Ingestion.Wal;

public interface IIngestionAcceptanceLog
{
    Task<IngestionWalAppendResult> AppendAsync(
        IngestionWalRecordMetadata metadata,
        Stream payload,
        long payloadLength,
        CancellationToken cancellationToken = default);

    IEnumerable<string> EnumerateLaneFiles();
}