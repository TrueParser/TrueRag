using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Storage.Migrations;
using TrueRag.Storage.Persistence;

namespace TrueRag.Storage;

public static class StorageModule
{
    public static IServiceCollection AddTrueRagStorage(
        this IServiceCollection services,
        string writeConnectionString,
        string readConnectionString,
        DatabaseEngine writeEngine = DatabaseEngine.CrateDb,
        DatabaseEngine readEngine = DatabaseEngine.CrateDb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(writeConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(readConnectionString);

        services.AddSingleton(new StorageDataSources(writeConnectionString, readConnectionString));
        services.AddSingleton<ISchemaMigrationService>(sp =>
            new SchemaMigrationService(
                sp.GetRequiredService<StorageDataSources>(),
                writeEngine));
        services.AddSingleton<IStorageHealthProbe, StorageHealthProbe>();
        services.AddScoped<IIngestionRepository>(sp =>
            new IngestionRepository(
                sp.GetRequiredService<StorageDataSources>(),
                StorageSqlDialect.Create(writeEngine)));
        services.AddScoped<IConversationRepository>(sp =>
            new ConversationRepository(
                sp.GetRequiredService<StorageDataSources>()));
        services.AddScoped<IRetrievalRepository>(sp =>
            new RetrievalRepository(
                sp.GetRequiredService<StorageDataSources>(),
                StorageSqlDialect.Create(readEngine)));
        services.AddScoped<IActiveEmbeddingProfileStore, EmbeddingProfileStore>();

        return services;
    }
}
