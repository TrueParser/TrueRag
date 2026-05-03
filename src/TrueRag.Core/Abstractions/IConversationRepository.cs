using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface IConversationRepository
{
    Task<Result> AppendTurnAsync(
        IRequestContext requestContext,
        ConversationTurn turn,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<ConversationTurn>>> GetThreadAsync(
        IRequestContext requestContext,
        string threadId,
        CancellationToken cancellationToken = default);
}