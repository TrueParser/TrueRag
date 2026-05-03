namespace TrueRag.Ingestion.Admission;

public sealed class IngestionBackpressureOptions
{
    public const string SectionName = "IngestionBackpressure";

    public int MaxFamilyQueueDepth { get; set; } = 1024;

    public double DrainCapacityRatioRejectThreshold { get; set; } = 1.4;

    public int MinDepthBeforeDrainRatioReject { get; set; } = 32;

    public int MinSampleWindowSeconds { get; set; } = 5;
}
