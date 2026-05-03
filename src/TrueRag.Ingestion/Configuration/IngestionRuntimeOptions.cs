namespace TrueRag.Ingestion.Configuration;

public sealed class IngestionRuntimeOptions
{
    public const string SectionName = "IngestionRuntime";

    public string NodeId { get; set; } = Environment.MachineName;

    public string WalRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "wal");

    public int SyncMaxConcurrency { get; set; } = 8;

    public bool WalFsync { get; set; }

    public long WalMaxSegmentBytes { get; set; } = 64L * 1024L * 1024L;

    public int WorkerBatchSize { get; set; } = 16;

    public int WorkerBatchWaitMs { get; set; } = 200;
}
