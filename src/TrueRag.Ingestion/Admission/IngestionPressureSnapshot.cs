namespace TrueRag.Ingestion.Admission;

public sealed record IngestionPressureSnapshot(
    double DrainCapacityRatio,
    double AcceptedItemsPerSec,
    double DrainedItemsPerSec,
    int TotalLiveDepth,
    DateTime SampledAtUtc);
