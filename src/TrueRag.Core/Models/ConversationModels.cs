namespace TrueRag.Core.Models;

public sealed record ConversationTurn(
    string ThreadId,
    string UserMessage,
    DateTimeOffset OccurredAtUtc,
    string? ActiveDocumentId = null,
    string? ActiveSectionPath = null);

public sealed record ConversationMessage(
    string ThreadId,
    string Role,
    string Message,
    DateTimeOffset OccurredAtUtc,
    string? ActiveDocumentId = null,
    string? ActiveSectionPath = null);

public sealed record ConversationThreadState(
    string ThreadId,
    string? Summary,
    string? ActiveDocumentId,
    string? ActiveSectionPath,
    DateTimeOffset LastRefreshedAtUtc,
    int TotalTurns);

public sealed record ConversationThreadSnapshot(
    string ThreadId,
    IReadOnlyCollection<ConversationMessage> Messages,
    ConversationThreadState State);

public sealed record ConversationReply(
    string ThreadId,
    string AssistantMessage,
    ConversationThreadSnapshot Snapshot,
    IReadOnlyCollection<LlmToolCall>? ToolCalls = null,
    string? Provider = null,
    double? LlmCertainty = null);

public sealed record ConversationGenerateRequest(
    string ThreadId,
    string UserMessage,
    IReadOnlyCollection<RetrievedContextItem> RetrievedContext,
    string? Provider = null,
    int? PromptTokenBudget = null);

public sealed record RetrievedContextItem(
    string NodeId,
    string Text,
    string? SourceDocumentId = null,
    double? Score = null);
