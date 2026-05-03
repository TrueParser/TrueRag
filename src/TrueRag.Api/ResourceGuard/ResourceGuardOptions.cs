using System.ComponentModel.DataAnnotations;

namespace TrueRag.Api.ResourceGuard;

public sealed class ResourceGuardOptions
{
    public const string SectionName = "ResourceGuard";

    public bool Enabled { get; set; } = true;

    [Range(100, 60_000)]
    public int SampleIntervalMs { get; set; } = 1_000;

    [Range(0, 100)]
    public double MemoryDegradedPercent { get; set; } = 75;

    [Range(0, 100)]
    public double MemoryOverloadedPercent { get; set; } = 90;

    [Range(0, 100)]
    public double CpuDegradedPercent { get; set; } = 75;

    [Range(0, 100)]
    public double CpuOverloadedPercent { get; set; } = 90;

    [Range(0, int.MaxValue)]
    public int ThreadPoolQueuePerCoreDegradedThreshold { get; set; } = 20;

    [Range(0, int.MaxValue)]
    public int ThreadPoolQueuePerCoreOverloadedThreshold { get; set; } = 50;

    [Range(0, long.MaxValue)]
    public long ActiveRequestsDegradedThreshold { get; set; } = 500;

    [Range(0, long.MaxValue)]
    public long ActiveRequestsOverloadedThreshold { get; set; } = 1000;

    [Range(0, double.MaxValue)]
    public double DrainCapacityRatioDegradedThreshold { get; set; } = 1.3;

    [Range(0, double.MaxValue)]
    public double DrainCapacityRatioOverloadedThreshold { get; set; } = 1.8;

    [Range(0, int.MaxValue)]
    public int LiveQueueDepthDegradedThreshold { get; set; } = 256;

    [Range(0, int.MaxValue)]
    public int LiveQueueDepthOverloadedThreshold { get; set; } = 1024;

    [Range(1, 20)]
    public int ConsecutiveSamplesForOverload { get; set; } = 2;

    [Range(1, 20)]
    public int ConsecutiveSamplesForRecovery { get; set; } = 3;

    [Range(0, 300_000)]
    public int MinimumOverloadedDurationMs { get; set; } = 1500;

    [Range(0, 600)]
    public int RetryAfterOverloadedSeconds { get; set; } = 15;

    public string[] BypassPaths { get; set; } =
    [
        "/health/live",
        "/health/ready"
    ];
}
