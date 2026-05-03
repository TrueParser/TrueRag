using TrueRag.Core.Context;

namespace TrueRag.Conversations;

internal interface IConversationStateStore
{
    Task SetAsync(
        IRequestContext requestContext,
        string threadId,
        string? activeDocumentId,
        string? activeSectionPath,
        CancellationToken cancellationToken = default);

    Task<ConversationEphemeralState?> GetAsync(
        IRequestContext requestContext,
        string threadId,
        CancellationToken cancellationToken = default);
}

internal sealed record ConversationEphemeralState(
    string? ActiveDocumentId,
    string? ActiveSectionPath,
    DateTimeOffset UpdatedAtUtc);
