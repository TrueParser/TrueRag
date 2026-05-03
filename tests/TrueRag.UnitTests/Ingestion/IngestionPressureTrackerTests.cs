using TrueRag.Ingestion.Admission;

namespace TrueRag.UnitTests.Ingestion;

public sealed class IngestionPressureTrackerTests
{
    [Fact]
    public void TryReserve_EnforcesMaxDepth_AndTerminalReleaseFreesSlot()
    {
        var tracker = new IngestionPressureTracker();
        var first = tracker.TryReserve("t1", "a1", "f1", "d1", 1, out _, out var firstReservedNew);
        var second = tracker.TryReserve("t1", "a1", "f1", "d2", 1, out _, out _);

        Assert.True(first);
        Assert.True(firstReservedNew);
        Assert.False(second);

        var released = tracker.MarkTerminal("t1", "a1", "f1", "d1");
        Assert.True(released);

        var third = tracker.TryReserve("t1", "a1", "f1", "d3", 1, out _, out _);
        Assert.True(third);
    }

    [Fact]
    public void CaptureSnapshot_ComputesDrainRatio()
    {
        var tracker = new IngestionPressureTracker();
        tracker.RecordAccepted();
        tracker.RecordAccepted();
        tracker.RecordDrained();

        Thread.Sleep(20);
        var snapshot = tracker.CaptureSnapshot();

        Assert.True(snapshot.AcceptedItemsPerSec > 0);
        Assert.True(snapshot.DrainedItemsPerSec > 0);
        Assert.True(snapshot.DrainCapacityRatio > 1.0);
    }
}
