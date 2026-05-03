using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface IConversationService
{
    Task<Result<ConversationReply>> GenerateReplyAsync(
        IRequestContext requestContext,
        ConversationGenerateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ConversationThreadSnapshot>> AddTurnAsync(
        IRequestContext requestContext,
        ConversationTurn turn,
        CancellationToken cancellationToken = default);

    Task<Result<ConversationThreadSnapshot>> GetThreadAsync(
        IRequestContext requestContext,
        string threadId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<Result<ConversationThreadSnapshot>> RefreshThreadStateAsync(
        IRequestContext requestContext,
        string threadId,
        int recentWindow = 12,
        CancellationToken cancellationToken = default);
}
