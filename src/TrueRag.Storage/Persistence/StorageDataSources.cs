using Npgsql;

namespace TrueRag.Storage.Persistence;

public sealed class StorageDataSources : IAsyncDisposable
{
    public StorageDataSources(string writeConnectionString, string readConnectionString)
    {
        Write = NpgsqlDataSource.Create(writeConnectionString);
        Read = NpgsqlDataSource.Create(readConnectionString);
    }

    public NpgsqlDataSource Write { get; }

    public NpgsqlDataSource Read { get; }

    public async ValueTask DisposeAsync()
    {
        await Write.DisposeAsync();
        await Read.DisposeAsync();
    }
}