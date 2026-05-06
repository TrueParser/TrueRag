using Microsoft.Extensions.DependencyInjection;
using TrueRag.IntegrationTests.Infrastructure;
using TrueRag.Storage;
using TrueRag.Storage.Migrations;
using TrueRag.Storage.Persistence;

namespace TrueRag.IntegrationTests.Storage;

public sealed class SchemaMigrationIntegrationTests : IClassFixture<RawPostgreSqlIntegrationFixture>
{
    private readonly RawPostgreSqlIntegrationFixture _fixture;

    public SchemaMigrationIntegrationTests(RawPostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApplyPendingAsync_FreshBootstrap_CreatesCoreSchema()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        using var provider = BuildProvider(_fixture.ConnectionString);
        var migration = provider.GetRequiredService<ISchemaMigrationService>();

        var initialPlan = await migration.GetPlanAsync();
        Assert.NotEmpty(initialPlan.Pending);

        var afterApply = await migration.ApplyPendingAsync();
        Assert.Empty(afterApply.Pending);

        Assert.True(await _fixture.TableExistsAsync("nodes"));
        Assert.True(await _fixture.TableExistsAsync("conversation_messages"));
        Assert.True(await _fixture.TableExistsAsync("conversation_thread_states"));
        Assert.True(await _fixture.TableExistsAsync("embedding_active_profiles"));
        Assert.True(await _fixture.TableExistsAsync("schema_migrations"));
        Assert.True(await _fixture.CountAppliedMigrationsAsync() > 0);
    }

    [Fact]
    public async Task ApplyPendingAsync_ReRun_IsIdempotent_WithNoAdditionalMigrations()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        using var provider = BuildProvider(_fixture.ConnectionString);
        var migration = provider.GetRequiredService<ISchemaMigrationService>();

        await migration.ApplyPendingAsync();
        var firstCount = await _fixture.CountAppliedMigrationsAsync();

        var afterSecondRun = await migration.ApplyPendingAsync();
        var secondCount = await _fixture.CountAppliedMigrationsAsync();

        Assert.Empty(afterSecondRun.Pending);
        Assert.Equal(firstCount, secondCount);
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddTrueRagStorage(
            writeConnectionString: connectionString,
            readConnectionString: connectionString,
            writeEngine: DatabaseEngine.PostgreSql,
            readEngine: DatabaseEngine.PostgreSql);
        return services.BuildServiceProvider();
    }
}
