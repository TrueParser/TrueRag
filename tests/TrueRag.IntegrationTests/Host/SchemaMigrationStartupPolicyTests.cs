using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Host.Migrations;
using TrueRag.Storage.Migrations;

namespace TrueRag.IntegrationTests.Host;

public sealed class SchemaMigrationStartupPolicyTests
{
    [Fact]
    public async Task EnforceAsync_AutoMigrateEnabled_AppliesPending()
    {
        var fake = new FakeSchemaMigrationService(
            pending: [new SchemaMigrationDefinition("0001", "init", "SELECT 1;")]);
        var services = new ServiceCollection();
        services.AddSingleton<ISchemaMigrationService>(fake);
        services.AddSingleton<IOptions<SchemaMigrationStartupOptions>>(Options.Create(new SchemaMigrationStartupOptions
        {
            AutoMigrateOnStartup = true,
            FailFastOnPendingMigrations = true
        }));

        using var provider = services.BuildServiceProvider();
        await SchemaMigrationStartupPolicy.EnforceAsync(provider, NullLogger.Instance);

        Assert.Equal(1, fake.ApplyCalls);
    }

    [Fact]
    public async Task EnforceAsync_PendingAndFailFast_Throws()
    {
        var fake = new FakeSchemaMigrationService(
            pending: [new SchemaMigrationDefinition("0002", "next", "SELECT 2;")]);
        var services = new ServiceCollection();
        services.AddSingleton<ISchemaMigrationService>(fake);
        services.AddSingleton<IOptions<SchemaMigrationStartupOptions>>(Options.Create(new SchemaMigrationStartupOptions
        {
            AutoMigrateOnStartup = false,
            FailFastOnPendingMigrations = true
        }));

        using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SchemaMigrationStartupPolicy.EnforceAsync(provider, NullLogger.Instance));
    }

    [Fact]
    public async Task EnforceAsync_PendingAndFailFastDisabled_DoesNotThrow()
    {
        var fake = new FakeSchemaMigrationService(
            pending: [new SchemaMigrationDefinition("0003", "next", "SELECT 3;")]);
        var services = new ServiceCollection();
        services.AddSingleton<ISchemaMigrationService>(fake);
        services.AddSingleton<IOptions<SchemaMigrationStartupOptions>>(Options.Create(new SchemaMigrationStartupOptions
        {
            AutoMigrateOnStartup = false,
            FailFastOnPendingMigrations = false
        }));

        using var provider = services.BuildServiceProvider();
        await SchemaMigrationStartupPolicy.EnforceAsync(provider, NullLogger.Instance);

        Assert.Equal(0, fake.ApplyCalls);
    }

    private sealed class FakeSchemaMigrationService : ISchemaMigrationService
    {
        private readonly IReadOnlyCollection<SchemaMigrationDefinition> _pending;

        public FakeSchemaMigrationService(IReadOnlyCollection<SchemaMigrationDefinition> pending)
        {
            _pending = pending;
        }

        public int ApplyCalls { get; private set; }

        public Task<SchemaMigrationPlan> GetPlanAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SchemaMigrationPlan(_pending, []));

        public Task<IReadOnlyCollection<SchemaMigrationDrift>> ValidateChecksumsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SchemaMigrationDrift>>([]);

        public Task<SchemaMigrationPlan> ApplyPendingAsync(CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            return Task.FromResult(new SchemaMigrationPlan([], [
                new AppliedSchemaMigration("0001", "checksum", DateTimeOffset.UtcNow)
            ]));
        }
    }
}
