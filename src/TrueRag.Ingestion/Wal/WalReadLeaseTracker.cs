using System.Collections.Concurrent;

namespace TrueRag.Ingestion.Wal;

internal sealed class WalReadLeaseTracker : IWalReadLeaseTracker
{
    private readonly ConcurrentDictionary<string, int> _leases = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable Acquire(string walPath)
    {
        _leases.AddOrUpdate(walPath, 1, static (_, count) => count + 1);
        return new Releaser(_leases, walPath);
    }

    public bool IsLeased(string walPath)
        => _leases.TryGetValue(walPath, out var count) && count > 0;

    private sealed class Releaser : IDisposable
    {
        private readonly ConcurrentDictionary<string, int> _leases;
        private readonly string _walPath;
        private int _disposed;

        public Releaser(ConcurrentDictionary<string, int> leases, string walPath)
        {
            _leases = leases;
            _walPath = walPath;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _leases.AddOrUpdate(
                _walPath,
                0,
                static (_, count) => count <= 1 ? 0 : count - 1);

            if (_leases.TryGetValue(_walPath, out var count) && count == 0)
            {
                _leases.TryRemove(_walPath, out _);
            }
        }
    }
}
