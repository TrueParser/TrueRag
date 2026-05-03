using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface IConversationService
{
    Task<Result<ConversationReply>> GenerateReplyAsync(
        IRequestContext requestContext,
        ConversationTurn turn,
        CancellationToken cancellationToken = default);
}