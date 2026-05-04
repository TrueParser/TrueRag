namespace TrueRag.Ingestion.Wal;

public sealed record IngestionWalAppendResult(
    string TenantId,
    string AppId,
    string CollectionId,
    string LaneKey,
    string WalPath,
    string WalSegmentId,
    long Offset,
    long Length,
    int ShardIndex);
