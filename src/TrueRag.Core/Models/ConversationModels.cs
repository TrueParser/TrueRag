namespace TrueRag.Core.Models;

public sealed record ConversationTurn(
    string ThreadId,
    string UserMessage,
    DateTimeOffset OccurredAtUtc);

public sealed record ConversationReply(
    string ThreadId,
    string AssistantMessage);