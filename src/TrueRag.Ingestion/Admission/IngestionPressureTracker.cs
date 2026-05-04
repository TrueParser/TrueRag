using System.Collections.Concurrent;
using System.Diagnostics;

namespace TrueRag.Ingestion.Admission;

public sealed class IngestionPressureTracker : IIngestionPressureTracker
{
    private readonly ConcurrentDictionary<string, FamilyBucket> _buckets = new(StringComparer.OrdinalIgnoreCase);
    private long _acceptedCount;
    private long _drainedCount;
    private long _lastAcceptedCount;
    private long _lastDrainedCount;
    private long _lastSampleTicks = Stopwatch.GetTimestamp();

    public bool TryReserve(
        string tenantId,
        string appId,
        string collectionId,
        string familyKey,
        string documentId,
        int maxDepth,
        out int currentDepth,
        out bool reservedNew)
    {
        ValidateIdentity(tenantId, appId, collectionId, familyKey, documentId);

        var bucket = _buckets.GetOrAdd(BuildBucketKey(tenantId, appId, collectionId, familyKey), _ => new FamilyBucket());
        lock (bucket.Gate)
        {
            if (bucket.Reservations.TryGetValue(documentId, out var state))
            {
                state.Touch();
                currentDepth = bucket.LiveDepth;
                reservedNew = false;
                return true;
            }

            if (maxDepth > 0 && bucket.LiveDepth >= maxDepth)
            {
                currentDepth = bucket.LiveDepth;
                reservedNew = false;
                return false;
            }

            bucket.Reservations[documentId] = new ReservationState(ReservationStatus.Pending);
            bucket.LiveDepth++;
            currentDepth = bucket.LiveDepth;
            reservedNew = true;
            return true;
        }
    }

    public bool Release(string tenantId, string appId, string collectionId, string familyKey, string documentId)
    {
        ValidateIdentity(tenantId, appId, collectionId, familyKey, documentId);
        if (!_buckets.TryGetValue(BuildBucketKey(tenantId, appId, collectionId, familyKey), out var bucket))
        {
            return false;
        }

        lock (bucket.Gate)
        {
            if (!bucket.Reservations.TryGetValue(documentId, out var reservation))
            {
                return false;
            }

            if (reservation.Status != ReservationStatus.Pending)
            {
                reservation.Touch();
                return false;
            }

            bucket.Reservations.Remove(documentId);
            bucket.LiveDepth = Math.Max(0, bucket.LiveDepth - 1);
            return true;
        }
    }

    public bool MarkPublished(string tenantId, string appId, string collectionId, string familyKey, string documentId)
    {
        ValidateIdentity(tenantId, appId, collectionId, familyKey, documentId);
        if (!_buckets.TryGetValue(BuildBucketKey(tenantId, appId, collectionId, familyKey), out var bucket))
        {
            return false;
        }

        lock (bucket.Gate)
        {
            if (!bucket.Reservations.TryGetValue(documentId, out var reservation))
            {
                return false;
            }

            reservation.Touch();
            if (reservation.Status == ReservationStatus.Pending)
            {
                reservation.Status = ReservationStatus.Published;
            }

            return true;
        }
    }

    public bool MarkTerminal(string tenantId, string appId, string collectionId, string familyKey, string documentId)
    {
        ValidateIdentity(tenantId, appId, collectionId, familyKey, documentId);
        if (!_buckets.TryGetValue(BuildBucketKey(tenantId, appId, collectionId, familyKey), out var bucket))
        {
            return false;
        }

        lock (bucket.Gate)
        {
            if (!bucket.Reservations.TryGetValue(documentId, out var reservation))
            {
                return false;
            }

            reservation.Touch();
            if (reservation.Status == ReservationStatus.TerminalReleased)
            {
                return false;
            }

            reservation.Status = ReservationStatus.TerminalReleased;
            bucket.Reservations.Remove(documentId);
            bucket.LiveDepth = Math.Max(0, bucket.LiveDepth - 1);
            return true;
        }
    }

    public int GetDepth(string tenantId, string appId, string collectionId, string familyKey)
    {
        if (!_buckets.TryGetValue(BuildBucketKey(tenantId, appId, collectionId, familyKey), out var bucket))
        {
            return 0;
        }

        lock (bucket.Gate)
        {
            return bucket.LiveDepth;
        }
    }

    public int GetTotalLiveDepth()
    {
        var total = 0;
        foreach (var bucket in _buckets.Values)
        {
            lock (bucket.Gate)
            {
                total += bucket.LiveDepth;
            }
        }

        return total;
    }

    public void RecordAccepted()
        => Interlocked.Increment(ref _acceptedCount);

    public void RecordDrained()
        => Interlocked.Increment(ref _drainedCount);

    public IngestionPressureSnapshot CaptureSnapshot()
    {
        var nowTicks = Stopwatch.GetTimestamp();
        var lastTicks = Interlocked.Exchange(ref _lastSampleTicks, nowTicks);
        var elapsedSeconds = Math.Max((nowTicks - lastTicks) / (double)Stopwatch.Frequency, 1e-6);

        var acceptedNow = Interlocked.Read(ref _acceptedCount);
        var drainedNow = Interlocked.Read(ref _drainedCount);
        var acceptedDelta = acceptedNow - Interlocked.Exchange(ref _lastAcceptedCount, acceptedNow);
        var drainedDelta = drainedNow - Interlocked.Exchange(ref _lastDrainedCount, drainedNow);

        var acceptedPerSec = Math.Max(0, acceptedDelta / elapsedSeconds);
        var drainedPerSec = Math.Max(0, drainedDelta / elapsedSeconds);
        var ratio = drainedPerSec <= 0
            ? (acceptedPerSec > 0 ? double.PositiveInfinity : 0d)
            : acceptedPerSec / drainedPerSec;

        return new IngestionPressureSnapshot(
            ratio,
            acceptedPerSec,
            drainedPerSec,
            GetTotalLiveDepth(),
            DateTime.UtcNow);
    }

    private static string BuildBucketKey(string tenantId, string appId, string collectionId, string familyKey)
        => $"{tenantId}:{appId}:{collectionId}:{familyKey}";

    private static void ValidateIdentity(string tenantId, string appId, string collectionId, string familyKey, string documentId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("tenantId is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentException("appId is required.", nameof(appId));
        }

        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new ArgumentException("collectionId is required.", nameof(collectionId));
        }

        if (string.IsNullOrWhiteSpace(familyKey))
        {
            throw new ArgumentException("familyKey is required.", nameof(familyKey));
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId is required.", nameof(documentId));
        }
    }

    private sealed class FamilyBucket
    {
        public object Gate { get; } = new();
        public Dictionary<string, ReservationState> Reservations { get; } = new(StringComparer.Ordinal);
        public int LiveDepth { get; set; }
    }

    private sealed class ReservationState(ReservationStatus status)
    {
        public ReservationStatus Status { get; set; } = status;
        public DateTime LastTouchedUtc { get; private set; } = DateTime.UtcNow;

        public void Touch()
            => LastTouchedUtc = DateTime.UtcNow;
    }

    private enum ReservationStatus
    {
        Pending,
        Published,
        TerminalReleased
    }
}
