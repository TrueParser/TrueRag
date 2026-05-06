using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrueRag.Host.Migrations;
using TrueRag.Storage.Migrations;

namespace TrueRag.IntegrationTests.Host;

public sealed class MigrationCommandHandlerTests
{
    [Theory]
    [InlineData(new[] { "migrate", "up" }, "up")]
    [InlineData(new[] { "migrate", "status" }, "status")]
    [InlineData(new[] { "migrate", "validate" }, "validate")]
    public void TryParse_RecognizesSupportedCommands(string[] args, string expected)
    {
        var parsed = MigrationCommandHandler.TryParse(args, out var command);

        Assert.True(parsed);
        Assert.Equal(expected, command);
    }

    [Fact]
    public async Task TryHandleAsync_Status_UsesMigrationServiceAndReturnsHandled()
    {
        var fake = new FakeSchemaMigrationService();
        var services = new ServiceCollection();
        services.AddSingleton<ISchemaMigrationService>(fake);
        using var provider = services.BuildServiceProvider();

        var handled = await MigrationCommandHandler.TryHandleAsync(
            ["migrate", "status"],
            provider,
            NullLogger.Instance);

        Assert.True(handled);
        Assert.Equal(1, fake.StatusCalls);
    }

    [Fact]
    public async Task TryHandleAsync_Up_AppliesPendingAndReturnsHandled()
    {
        var fake = new FakeSchemaMigrationService();
        var services = new ServiceCollection();
        services.AddSingleton<ISchemaMigrationService>(fake);
        using var provider = services.BuildServiceProvider();

        var handled = await MigrationCommandHandler.TryHandleAsync(
            ["migrate", "up"],
            provider,
            NullLogger.Instance);

        Assert.True(handled);
        Assert.Equal(1, fake.ApplyCalls);
    }

    private sealed class FakeSchemaMigrationService : ISchemaMigrationService
    {
        public int StatusCalls { get; private set; }

        public int ApplyCalls { get; private set; }

        public Task<SchemaMigrationPlan> GetPlanAsync(CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            return Task.FromResult(new SchemaMigrationPlan(
                [new SchemaMigrationDefinition("0001", "init", "SELECT 1;")],
                []));
        }

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
