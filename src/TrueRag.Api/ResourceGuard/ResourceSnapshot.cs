namespace TrueRag.Api.ResourceGuard;

public sealed record ResourceSnapshot(
    double MemoryPercent,
    double CpuPercent,
    long ThreadPoolQueueDepth,
    long ActiveRequests,
    double DrainCapacityRatio,
    double AcceptedItemsPerSec,
    double DrainedItemsPerSec,
    int LiveQueueDepth,
    NodeState State,
    string? DegradationReason,
    DateTime SampledAtUtc);
