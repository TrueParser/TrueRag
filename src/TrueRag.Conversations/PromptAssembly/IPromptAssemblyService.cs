using TrueRag.Core.Models;

namespace TrueRag.Conversations.PromptAssembly;

internal interface IPromptAssemblyService
{
    PromptAssemblyResult Assemble(
        ConversationGenerateRequest request,
        ConversationThreadSnapshot snapshot);
}

internal sealed record PromptAssemblyResult(
    IReadOnlyCollection<LlmMessage> Messages,
    int EstimatedPromptTokens,
    int BudgetUsed,
    int BudgetTotal);
