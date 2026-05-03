using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.IntegrationTests.Infrastructure;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;

namespace TrueRag.IntegrationTests.Conversations;

public sealed class ConversationPersistenceIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    private readonly PostgreSqlIntegrationFixture _fixture;

    public ConversationPersistenceIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConversationRepository_EnforcesTenantAppIsolation_ForThreadAndState()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateAsync();
        using var provider = BuildProvider();
        var repository = provider.GetRequiredService<IConversationRepository>();

        var ctxA = new RequestContext("tenant-a", "app-a", "u1", ["reader"], ["g1"]);
        var ctxB = new RequestContext("tenant-b", "app-a", "u2", ["reader"], ["g1"]);
        var threadId = "thread-123";

        var appendA = await repository.AppendMessageAsync(
            ctxA,
            new ConversationMessage(threadId, "user", "hello from tenant A", DateTimeOffset.UtcNow, "doc-a", "Document/Section1"));
        var appendB = await repository.AppendMessageAsync(
            ctxB,
            new ConversationMessage(threadId, "user", "hello from tenant B", DateTimeOffset.UtcNow, "doc-b", "Document/Section2"));

        Assert.True(appendA.IsSuccess);
        Assert.True(appendB.IsSuccess);

        var upsertA = await repository.UpsertThreadStateAsync(
            ctxA,
            new ConversationThreadState(threadId, "summary-a", "doc-a", "Document/Section1", DateTimeOffset.UtcNow, 1));
        var upsertB = await repository.UpsertThreadStateAsync(
            ctxB,
            new ConversationThreadState(threadId, "summary-b", "doc-b", "Document/Section2", DateTimeOffset.UtcNow, 1));

        Assert.True(upsertA.IsSuccess);
        Assert.True(upsertB.IsSuccess);

        var threadForA = await repository.GetThreadAsync(ctxA, threadId, 50);
        var stateForA = await repository.GetThreadStateAsync(ctxA, threadId);

        Assert.True(threadForA.IsSuccess);
        Assert.True(stateForA.IsSuccess);
        Assert.Single(threadForA.Value!);
        Assert.Equal("hello from tenant A", threadForA.Value!.Single().Message);
        Assert.Equal("summary-a", stateForA.Value!.Summary);
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddTrueRagStorage(
            writeConnectionString: _fixture.ConnectionString,
            readConnectionString: _fixture.ConnectionString,
            writeEngine: DatabaseEngine.PostgreSql,
            readEngine: DatabaseEngine.PostgreSql);
        return services.BuildServiceProvider();
    }

    private async Task TruncateAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "TRUNCATE TABLE conversation_messages, conversation_thread_states, nodes",
            connection);
        await command.ExecuteNonQueryAsync();
    }
}
