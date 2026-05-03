namespace TrueRag.Ingestion.Wal;

public sealed record IngestionWalRecordMetadata(
    string TenantId,
    string AppId,
    string DocumentId,
    string CorrelationId,
    string NodeId);