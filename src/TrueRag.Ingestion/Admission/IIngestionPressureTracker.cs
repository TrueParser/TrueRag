namespace TrueRag.Ingestion.Admission;

public interface IIngestionPressureTracker : IFamilyQueueDepthTracker, IIngestionPressureSnapshotProvider
{
    void RecordAccepted();

    void RecordDrained();
}
