using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Storage;
using TrueRag.Storage.Migrations;
using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class StorageModuleRegistrationTests
{
    [Theory]
    [InlineData(DatabaseEngine.CrateDb, DatabaseEngine.CrateDb)]
    [InlineData(DatabaseEngine.CrateDb, DatabaseEngine.PostgreSql)]
    [InlineData(DatabaseEngine.PostgreSql, DatabaseEngine.CrateDb)]
    [InlineData(DatabaseEngine.PostgreSql, DatabaseEngine.PostgreSql)]
    public void AddTrueRagStorage_RegistersRepositoriesAndHealthProbe(DatabaseEngine writeEngine, DatabaseEngine readEngine)
    {
        var services = new ServiceCollection();
        services.AddTrueRagStorage(
            writeConnectionString: "Host=localhost;Port=5432;Database=x;Username=x;Password=x",
            readConnectionString: "Host=localhost;Port=5432;Database=x;Username=x;Password=x",
            writeEngine: writeEngine,
            readEngine: readEngine);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIngestionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IConversationRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRetrievalRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStorageHealthProbe>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISchemaMigrationService>());
    }
}
