namespace TrueRag.Core.Models;

public sealed record LlmMessage(
    string Role,
    string Content);

public sealed record LlmToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

public sealed record LlmUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);

public sealed record LlmCompletionRequest(
    IReadOnlyCollection<LlmMessage> Messages,
    bool Stream = false,
    int? MaxOutputTokens = null,
    double? Temperature = null);

public sealed record LlmCompletionChunk(
    string DeltaText,
    bool IsFinal = false,
    IReadOnlyCollection<LlmToolCall>? ToolCalls = null,
    double? LlmCertainty = null);

public sealed record LlmCompletionResponse(
    string Text,
    IReadOnlyCollection<LlmToolCall> ToolCalls,
    LlmUsage Usage,
    string Provider,
    double? LlmCertainty = null);
