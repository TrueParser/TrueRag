using TrueRag.Api.Contracts;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

internal sealed class ConversationApiService : IConversationApiService
{
    private readonly IConversationService _conversationService;

    public ConversationApiService(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public Task<Result<ConversationThreadSnapshot>> GetThread(IRequestContext context, string threadId, int? take, CancellationToken cancellationToken = default)
        => _conversationService.GetThreadAsync(context, threadId, take ?? 50, cancellationToken);

    public Task<Result<ConversationThreadSnapshot>> RefreshThread(IRequestContext context, string threadId, int? recentWindow, CancellationToken cancellationToken = default)
        => _conversationService.RefreshThreadStateAsync(context, threadId, recentWindow ?? 12, cancellationToken);

    public Task<Result<ConversationReply>> Generate(IRequestContext context, RagGenerateInput input, CancellationToken cancellationToken = default)
    {
        var policyMode = input.PolicyMode ?? GenerationPolicyMode.Grounded;
        if (policyMode != GenerationPolicyMode.Grounded)
        {
            return Task.FromResult(
                Result<ConversationReply>.Failure(
                    new Error(
                        "conversation.grounded_route_requires_grounded_mode",
                        "The /api/v1/rag/generate route requires grounded generation mode.",
                        ErrorType.Validation)));
        }

        var request = new ConversationGenerateRequest(
            ThreadId: input.ThreadId,
            UserMessage: input.UserMessage,
            RetrievedContext: input.RetrievedContext ?? [],
            Provider: input.Provider,
            PromptTokenBudget: input.PromptTokenBudget,
            PolicyMode: policyMode);

        return _conversationService.GenerateReplyAsync(context, request, cancellationToken);
    }

    public Task<Result<ConversationThreadSnapshot>> AddTurn(IRequestContext context, string threadId, ConversationTurnInput input, CancellationToken cancellationToken = default)
    {
        var turn = new ConversationTurn(
            ThreadId: threadId,
            UserMessage: input.UserMessage,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            ActiveDocumentId: input.ActiveDocumentId,
            ActiveSectionPath: input.ActiveSectionPath);

        return _conversationService.AddTurnAsync(context, turn, cancellationToken);
    }
}
