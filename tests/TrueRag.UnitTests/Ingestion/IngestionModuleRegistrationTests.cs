using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Ingestion;
using TrueRag.Ingestion.Normalization;

namespace TrueRag.UnitTests.Ingestion;

public sealed class IngestionModuleRegistrationTests
{
    [Fact]
    public void AddTrueRagIngestion_RegistersNormalizer()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddTrueRagIngestion();

        using var provider = services.BuildServiceProvider();
        var normalizer = provider.GetRequiredService<IIngestionNormalizer>();

        Assert.NotNull(normalizer);
    }
}