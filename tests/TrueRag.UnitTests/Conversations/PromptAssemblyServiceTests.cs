using Microsoft.Extensions.Options;
using TrueRag.Conversations.Configuration;
using TrueRag.Conversations.PromptAssembly;
using TrueRag.Core.Models;

namespace TrueRag.UnitTests.Conversations;

public sealed class PromptAssemblyServiceTests
{
    [Fact]
    public void Assemble_RespectsBudget_AndKeepsRequiredPrioritySegments()
    {
        var options = Options.Create(new PromptAssemblyOptions
        {
            DefaultTokenBudget = 180,
            ReservedCompletionTokens = 60,
            SystemInstruction = "Always answer from context."
        });
        var service = new PromptAssemblyService(options);
        var snapshot = new ConversationThreadSnapshot(
            "t1",
            [
                new ConversationMessage("t1", "user", new string('a', 400), DateTimeOffset.UtcNow.AddMinutes(-2)),
                new ConversationMessage("t1", "assistant", new string('b', 400), DateTimeOffset.UtcNow.AddMinutes(-1))
            ],
            new ConversationThreadState("t1", new string('s', 300), null, null, DateTimeOffset.UtcNow, 2));

        var result = service.Assemble(
            new ConversationGenerateRequest(
                ThreadId: "t1",
                UserMessage: "current question",
                RetrievedContext:
                [
                    new RetrievedContextItem("n1", new string('c', 600), "doc", 0.9)
                ]),
            snapshot);

        Assert.NotEmpty(result.Messages);
        Assert.True(result.BudgetUsed <= result.BudgetTotal);
        Assert.Equal("system", result.Messages.First().Role);
        Assert.Contains(result.Messages, static message => message.Role == "user" && message.Content.Contains("current question", StringComparison.Ordinal));
    }

    [Fact]
    public void Assemble_WhenBudgetTight_PrefersSummaryOverHistory()
    {
        var options = Options.Create(new PromptAssemblyOptions
        {
            DefaultTokenBudget = 512,
            ReservedCompletionTokens = 320,
            SystemInstruction = "Ground only in provided context."
        });

        var service = new PromptAssemblyService(options);
        var now = DateTimeOffset.UtcNow;
        var snapshot = new ConversationThreadSnapshot(
            "t2",
            [
                new ConversationMessage("t2", "user", string.Join(' ', Enumerable.Repeat("history", 180)), now.AddMinutes(-3)),
                new ConversationMessage("t2", "assistant", string.Join(' ', Enumerable.Repeat("history", 180)), now.AddMinutes(-2))
            ],
            new ConversationThreadState("t2", string.Join(' ', Enumerable.Repeat("summary", 40)), null, null, now, 2));

        var result = service.Assemble(
            new ConversationGenerateRequest(
                ThreadId: "t2",
                UserMessage: "current question",
                RetrievedContext: [new RetrievedContextItem("n1", string.Join(' ', Enumerable.Repeat("context", 180)), "doc-1", 0.9)],
                PromptTokenBudget: 512),
            snapshot);

        Assert.True(result.BudgetUsed <= result.BudgetTotal);
        Assert.Contains(result.Messages, static m => m.Role == "system" && m.Content.Contains("[summary]", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Messages, static m => m.Content.StartsWith("history", StringComparison.Ordinal));
    }
}
