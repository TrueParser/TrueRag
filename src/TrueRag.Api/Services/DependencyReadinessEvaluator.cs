using TrueRag.Core.Abstractions;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

internal sealed class DependencyReadinessEvaluator : IDependencyReadinessEvaluator
{
    private readonly IStorageHealthProbe _storageHealthProbe;

    public DependencyReadinessEvaluator(IStorageHealthProbe storageHealthProbe)
    {
        _storageHealthProbe = storageHealthProbe;
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var readResult = await _storageHealthProbe.CheckReadStoreAsync(cancellationToken);
        var writeResult = await _storageHealthProbe.CheckWriteStoreAsync(cancellationToken);
        var storageReady = readResult.IsSuccess && writeResult.IsSuccess;
        states["storage_read"] = readResult.IsSuccess ? "ready" : "unavailable";
        states["storage_write"] = writeResult.IsSuccess ? "ready" : "unavailable";

        if (!storageReady)
        {
            return Result<IReadOnlyDictionary<string, string>>.Failure(
                new Error("health.readiness_failed", "One or more critical dependencies are unavailable.", ErrorType.Unavailable));
        }

        return Result<IReadOnlyDictionary<string, string>>.Success(states);
    }
}
