using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;
using TrueRag.IntegrationTests.Infrastructure;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;

namespace TrueRag.IntegrationTests.Storage;

public sealed class EmbeddingProfileStoreIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    private readonly PostgreSqlIntegrationFixture _fixture;

    public EmbeddingProfileStoreIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpsertAndGetActiveAsync_PersistsScopedProfile()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateAsync();
        using var provider = BuildProvider();
        var store = provider.GetRequiredService<IActiveEmbeddingProfileStore>();

        var now = DateTimeOffset.UtcNow;
        await store.UpsertActiveAsync(new ActiveEmbeddingProfileRecord(
            "tenant-a",
            "app-a",
            "collection-a",
            "onnx",
            "BAAI/bge-small-en-v1.5",
            384,
            512,
            EmbeddingDistanceMetric.Cosine,
            null,
            null,
            now));

        var active = await store.GetActiveAsync("tenant-a", "app-a", "collection-a");
        Assert.NotNull(active);
        Assert.Equal("onnx", active!.Provider);
        Assert.Equal(384, active.Dimensions);
    }

    [Fact]
    public async Task UpsertActiveAsync_WritesActivationHistory()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateAsync();
        using var provider = BuildProvider();
        var store = provider.GetRequiredService<IActiveEmbeddingProfileStore>();

        await store.UpsertActiveAsync(new ActiveEmbeddingProfileRecord(
            "tenant-a",
            "app-a",
            "collection-a",
            "onnx",
            "BAAI/bge-small-en-v1.5",
            384,
            512,
            EmbeddingDistanceMetric.Cosine,
            null,
            null,
            DateTimeOffset.UtcNow.AddMinutes(-1)));

        await store.UpsertActiveAsync(new ActiveEmbeddingProfileRecord(
            "tenant-a",
            "app-a",
            "collection-a",
            "onnx",
            "BAAI/bge-base-en-v1.5",
            768,
            512,
            EmbeddingDistanceMetric.Cosine,
            null,
            null,
            DateTimeOffset.UtcNow));

        var history = await store.GetActivationHistoryAsync("tenant-a", "app-a", "collection-a");
        Assert.True(history.Count >= 2);
        Assert.Equal("BAAI/bge-base-en-v1.5", history.First().Model);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_ReturnsFalseOnModelMismatch()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateAsync();
        using var provider = BuildProvider();
        var store = provider.GetRequiredService<IActiveEmbeddingProfileStore>();

        await store.UpsertActiveAsync(new ActiveEmbeddingProfileRecord(
            "tenant-a",
            "app-a",
            "collection-a",
            "onnx",
            "BAAI/bge-small-en-v1.5",
            384,
            512,
            EmbeddingDistanceMetric.Cosine,
            null,
            null,
            DateTimeOffset.UtcNow));

        var compatibility = await store.CheckCompatibilityAsync(
            "tenant-a",
            "app-a",
            "collection-a",
            "onnx",
            "BAAI/bge-base-en-v1.5",
            384);

        Assert.False(compatibility.IsCompatible);
        Assert.Equal("model_mismatch", compatibility.Reason);
    }

    [Fact]
    public async Task TransitionCrud_PersistsAndReturnsPendingTransition()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateAsync();
        using var provider = BuildProvider();
        var store = provider.GetRequiredService<IActiveEmbeddingProfileStore>();

        await store.UpsertActiveAsync(new ActiveEmbeddingProfileRecord(
            "tenant-a",
            "app-a",
            "collection-a",
            "onnx",
            "BAAI/bge-small-en-v1.5",
            384,
            512,
            EmbeddingDistanceMetric.Cosine,
            null,
            null,
            DateTimeOffset.UtcNow));

        var created = await store.CreateTransitionAsync(
            new EmbeddingProfileTransitionProposal(
                "tenant-a",
                "app-a",
                "collection-a",
                "onnx",
                "BAAI/bge-base-en-v1.5",
                768,
                512,
                EmbeddingDistanceMetric.Cosine,
                true,
                false),
            await store.GetActiveAsync("tenant-a", "app-a", "collection-a"));

        var pending = await store.GetLatestPendingTransitionAsync("tenant-a", "app-a", "collection-a");
        Assert.NotNull(pending);
        Assert.Equal(created.TransitionId, pending!.TransitionId);
        Assert.Equal(EmbeddingProfileTransitionStatus.Proposed, pending.Status);
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddTrueRagStorage(
            _fixture.ConnectionString,
            _fixture.ConnectionString,
            DatabaseEngine.PostgreSql,
            DatabaseEngine.PostgreSql);
        return services.BuildServiceProvider();
    }

    private async Task TruncateAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "TRUNCATE TABLE embedding_active_profiles, embedding_profile_activation_history, embedding_profile_transitions RESTART IDENTITY",
            connection);
        await command.ExecuteNonQueryAsync();
    }
}
