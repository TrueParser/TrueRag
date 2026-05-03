namespace TrueRag.Ingestion.Admission;

public interface IIngestionPressureSnapshotProvider
{
    IngestionPressureSnapshot CaptureSnapshot();
}
