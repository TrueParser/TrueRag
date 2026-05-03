using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface IConversationRepository
{
    Task<Result> AppendMessageAsync(
        IRequestContext requestContext,
        ConversationMessage message,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<ConversationMessage>>> GetThreadAsync(
        IRequestContext requestContext,
        string threadId,
        int take,
        CancellationToken cancellationToken = default);

    Task<Result<ConversationThreadState?>> GetThreadStateAsync(
        IRequestContext requestContext,
        string threadId,
        CancellationToken cancellationToken = default);

    Task<Result> UpsertThreadStateAsync(
        IRequestContext requestContext,
        ConversationThreadState state,
        CancellationToken cancellationToken = default);
}
