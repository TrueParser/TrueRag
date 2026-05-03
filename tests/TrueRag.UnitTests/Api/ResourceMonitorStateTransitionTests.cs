using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Api.ResourceGuard;
using TrueRag.Ingestion.Admission;

namespace TrueRag.UnitTests.Api;

public sealed class ResourceMonitorStateTransitionTests
{
    [Fact]
    public async Task Monitor_TransitionsToOverloaded_ThenRecoversToHealthy()
    {
        var options = Options.Create(new ResourceGuardOptions
        {
            Enabled = true,
            SampleIntervalMs = 100,
            DrainCapacityRatioOverloadedThreshold = 1.2,
            DrainCapacityRatioDegradedThreshold = 1.1,
            LiveQueueDepthOverloadedThreshold = 50,
            LiveQueueDepthDegradedThreshold = 10,
            ConsecutiveSamplesForOverload = 1,
            ConsecutiveSamplesForRecovery = 1,
            MinimumOverloadedDurationMs = 0
        });

        var provider = new MutablePressureProvider();
        using var monitor = new ResourceMonitor(options, provider, NullLogger<ResourceMonitor>.Instance);
        await monitor.StartAsync(CancellationToken.None);
        try
        {
            provider.Set(new IngestionPressureSnapshot(2.0, 100, 10, 100, DateTime.UtcNow));
            await Task.Delay(250);
            Assert.Equal(NodeState.Overloaded, monitor.Current.State);

            provider.Set(new IngestionPressureSnapshot(0.2, 10, 50, 1, DateTime.UtcNow));
            await Task.Delay(250);
            Assert.Equal(NodeState.Healthy, monitor.Current.State);
        }
        finally
        {
            await monitor.StopAsync(CancellationToken.None);
        }
    }

    private sealed class MutablePressureProvider : IIngestionPressureSnapshotProvider
    {
        private IngestionPressureSnapshot _snapshot = new(0, 0, 0, 0, DateTime.UtcNow);

        public void Set(IngestionPressureSnapshot snapshot)
            => _snapshot = snapshot;

        public IngestionPressureSnapshot CaptureSnapshot()
            => _snapshot with { SampledAtUtc = DateTime.UtcNow };
    }
}
