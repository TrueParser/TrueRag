using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TrueRag.Conversations;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.IntegrationTests.Infrastructure;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;

namespace TrueRag.IntegrationTests.Conversations;

public sealed class LlmOrchestrationIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    private readonly PostgreSqlIntegrationFixture _fixture;

    public LlmOrchestrationIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GenerateReplyAsync_OrchestratesProvider_AndPersistsAssistantTurn()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateAsync();
        using var provider = BuildProvider();

        var service = provider.GetRequiredService<IConversationService>();
        var repository = provider.GetRequiredService<IConversationRepository>();
        var context = new RequestContext("tenant-a", "app-a", "u1", ["reader"], ["g1"]);

        var response = await service.GenerateReplyAsync(
            context,
            new ConversationGenerateRequest(
                ThreadId: "thread-orch",
                UserMessage: "Find citation for section 9",
                RetrievedContext: [new RetrievedContextItem("n1", "Section 9 states obligations", "doc-1", 0.88)],
                Provider: "openai",
                PromptTokenBudget: 1000));

        Assert.True(response.IsSuccess);
        Assert.Equal("openai", response.Value!.Provider);
        Assert.NotNull(response.Value.ToolCalls);
        Assert.NotEmpty(response.Value.ToolCalls!);
        Assert.NotNull(response.Value.LlmCertainty);
        Assert.NotNull(response.Value.RetrievalConfidence);
        Assert.NotNull(response.Value.OverallConfidence);

        var thread = await repository.GetThreadAsync(context, "thread-orch", 50);
        Assert.True(thread.IsSuccess);
        Assert.Equal(2, thread.Value!.Count);
        Assert.Contains(thread.Value, static message => message.Role == "assistant");
    }

    private ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LlmProvider:DefaultProvider"] = "local",
                ["PromptAssembly:DefaultTokenBudget"] = "3000",
                ["PromptAssembly:ReservedCompletionTokens"] = "700"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddDistributedMemoryCache();
        services.AddTrueRagStorage(
            writeConnectionString: _fixture.ConnectionString,
            readConnectionString: _fixture.ConnectionString,
            writeEngine: DatabaseEngine.PostgreSql,
            readEngine: DatabaseEngine.PostgreSql);
        services.AddTrueRagConversations();
        return services.BuildServiceProvider();
    }

    private async Task TruncateAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("TRUNCATE TABLE conversation_messages, conversation_thread_states, nodes", connection);
        await command.ExecuteNonQueryAsync();
    }
}
