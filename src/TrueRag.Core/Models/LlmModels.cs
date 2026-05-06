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
    double? LlmCertainty = null,
    GroundedResponseContract? GroundedResponse = null,
    string? SchemaValidationErrorCode = null);

public enum GroundingStatus
{
    Grounded = 0,
    PartiallyGrounded = 1,
    InsufficientEvidence = 2,
    ConflictingEvidence = 3,
    ValidationFailed = 4
}

public sealed record GroundedClaim(
    string ClaimId,
    string Text,
    IReadOnlyCollection<string> CitationIds);

public sealed record GroundedCitation(
    string CitationId,
    string NodeId,
    string? DocumentId = null,
    string? TenantId = null,
    string? AppId = null,
    string? CollectionId = null,
    string? SectionPath = null,
    int? PageNumber = null,
    double? SupportScore = null,
    string? SpanId = null,
    int? StartOffset = null,
    int? EndOffset = null,
    string? Quote = null);

public sealed record GroundedResponseContract(
    string Answer,
    IReadOnlyCollection<GroundedClaim> Claims,
    IReadOnlyCollection<GroundedCitation> Citations,
    string? InsufficiencyReason,
    double? Confidence,
    GroundingStatus GroundingStatus);
