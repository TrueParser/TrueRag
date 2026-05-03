using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class RetrievalRepositoryValidationTests
{
    [Fact]
    public async Task QueryVectorAsync_ReturnsValidationError_WhenVectorMissing()
    {
        var repository = CreateRepository();
        var context = new RequestContext("tenant", "app", "user", [], []);
        var query = new RetrievalQuery("q", null, 5);

        var result = await repository.QueryVectorAsync(context, query);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.query_vector_missing", result.Error!.Code);
    }

    [Fact]
    public async Task QueryHybridAsync_ReturnsValidationError_WhenVectorMissing()
    {
        var repository = CreateRepository();
        var context = new RequestContext("tenant", "app", "user", [], []);
        var query = new RetrievalQuery("q", null, 5);

        var result = await repository.QueryHybridAsync(context, query);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.query_vector_missing", result.Error!.Code);
    }

    private static RetrievalRepository CreateRepository()
    {
        var dataSources = new StorageDataSources(
            "Host=localhost;Port=5432;Database=x;Username=x;Password=x",
            "Host=localhost;Port=5432;Database=x;Username=x;Password=x");

        return new RetrievalRepository(dataSources, StorageSqlDialect.Create(DatabaseEngine.CrateDb));
    }
}