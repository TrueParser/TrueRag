using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface IStorageHealthProbe
{
    Task<Result> CheckReadStoreAsync(CancellationToken cancellationToken = default);

    Task<Result> CheckWriteStoreAsync(CancellationToken cancellationToken = default);
}