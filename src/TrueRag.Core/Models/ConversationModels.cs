namespace TrueRag.Core.Models;

public enum GenerationPolicyMode
{
    Grounded = 0,
    Utility = 1
}

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
    double? LlmCertainty = null,
    double? RetrievalConfidence = null,
    double? OverallConfidence = null,
    IReadOnlyCollection<GroundedClaim>? Claims = null,
    IReadOnlyCollection<GroundedCitation>? Citations = null,
    string? InsufficiencyReason = null,
    GroundingStatus? GroundingStatus = null,
    GroundingDiagnostics? Diagnostics = null);

public sealed record GroundingDiagnostics(
    int RetrievalHitCount,
    IReadOnlyCollection<string> SelectedEvidenceNodeIds,
    string CitationValidationResult,
    string VerifierOutcome,
    string? AbstentionReason,
    int VerifierRetryCount,
    bool PromptInjectionDetected);

public sealed record ConversationGenerateRequest(
    string ThreadId,
    string UserMessage,
    IReadOnlyCollection<RetrievedContextItem> RetrievedContext,
    string? Provider = null,
    int? PromptTokenBudget = null,
    GenerationPolicyMode PolicyMode = GenerationPolicyMode.Grounded);

public sealed record RetrievedContextItem(
    string NodeId,
    string Text,
    string? SourceDocumentId = null,
    double? Score = null,
    string? TenantId = null,
    string? AppId = null,
    string? CollectionId = null,
    string? SectionPath = null,
    string? Title = null,
    int? PageNumber = null,
    string? SpanId = null,
    int? StartOffset = null,
    int? EndOffset = null,
    bool IsCiteable = true,
    IReadOnlyCollection<string>? AclScopes = null,
    DateTimeOffset? SourceUpdatedAtUtc = null,
    double? SourceAuthorityScore = null);
