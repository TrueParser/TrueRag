namespace TrueRag.Ingestion.Wal;

public interface IWalReadLeaseTracker
{
    IDisposable Acquire(string walPath);

    bool IsLeased(string walPath);
}

