using TrueRag.Core.Abstractions;
using TrueRag.Core.Primitives;
using TrueRag.Storage.Persistence;

namespace TrueRag.Storage;

internal sealed class StorageHealthProbe : IStorageHealthProbe
{
    private readonly StorageDataSources _dataSources;

    public StorageHealthProbe(StorageDataSources dataSources)
    {
        _dataSources = dataSources;
    }

    public async Task<Result> CheckReadStoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _dataSources.Read.OpenConnectionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("storage.read_unavailable", ex.Message, ErrorType.Unavailable));
        }
    }

    public async Task<Result> CheckWriteStoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _dataSources.Write.OpenConnectionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("storage.write_unavailable", ex.Message, ErrorType.Unavailable));
        }
    }
}