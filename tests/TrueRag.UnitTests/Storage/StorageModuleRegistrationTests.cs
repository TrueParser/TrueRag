using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class StorageModuleRegistrationTests
{
    [Fact]
    public void AddTrueRagStorage_RegistersRepositoriesAndHealthProbe()
    {
        var services = new ServiceCollection();
        services.AddTrueRagStorage(
            writeConnectionString: "Host=localhost;Port=5432;Database=x;Username=x;Password=x",
            readConnectionString: "Host=localhost;Port=5432;Database=x;Username=x;Password=x",
            writeEngine: DatabaseEngine.CrateDb,
            readEngine: DatabaseEngine.PostgreSql);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIngestionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRetrievalRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStorageHealthProbe>());
    }
}