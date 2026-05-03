using TrueRag.Api.Contracts;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

public interface IConversationApiService
{
    Task<Result<ConversationThreadSnapshot>> AddTurn(IRequestContext context, string threadId, ConversationTurnInput input, CancellationToken cancellationToken = default);

    Task<Result<ConversationThreadSnapshot>> GetThread(IRequestContext context, string threadId, int? take, CancellationToken cancellationToken = default);

    Task<Result<ConversationThreadSnapshot>> RefreshThread(IRequestContext context, string threadId, int? recentWindow, CancellationToken cancellationToken = default);

    Task<Result<ConversationReply>> Generate(IRequestContext context, RagGenerateInput input, CancellationToken cancellationToken = default);
}
