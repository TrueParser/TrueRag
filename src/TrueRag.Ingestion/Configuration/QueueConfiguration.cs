namespace TrueRag.Ingestion.Configuration;

public sealed class QueueConfiguration
{
    public const string SectionName = "Queue";

    public string Url { get; set; } = "nats://localhost:4222";

    public string StreamName { get; set; } = "TrueRagJob";

    public string SubjectPrefix { get; set; } = "TrueRAG.Job";

    public string IngestSubjectBase { get; set; } = "TrueRAG.Job.Ingest";
}
