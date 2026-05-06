using TrueRag.Conversations.PromptAssembly;
using Microsoft.Extensions.Options;
using TrueRag.Conversations.Configuration;
using System.Diagnostics;
using TrueRag.Conversations.Security;
using Microsoft.Extensions.Logging;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Conversations;

internal sealed class ConversationService : IConversationService
{
    private const int GroundedSchemaRetryLimit = 1;
    private const double MinimumTopScoreThreshold = 0.45d;
    private const double MinimumAverageScoreThreshold = 0.35d;
    private const double MinimumAlignmentThreshold = 0.20d;
    private const int MinimumDistinctSourceCountForStrongEvidence = 2;
    private static readonly char[] QueryTokenSeparators = [' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\''];

    private readonly IConversationRepository _repository;
    private readonly IConversationStateStore _stateStore;
    private readonly IConversationSummaryBuilder _summaryBuilder;
    private readonly IPromptAssemblyService _promptAssemblyService;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly GroundingGovernanceOptions _governanceOptions;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IConversationRepository repository,
        IConversationStateStore stateStore,
        IConversationSummaryBuilder summaryBuilder,
        IPromptAssemblyService promptAssemblyService,
        ILlmProviderFactory providerFactory,
        IOptions<GroundingGovernanceOptions> governanceOptions,
        ILogger<ConversationService> logger)
    {
        _repository = repository;
        _stateStore = stateStore;
        _summaryBuilder = summaryBuilder;
        _promptAssemblyService = promptAssemblyService;
        _providerFactory = providerFactory;
        _governanceOptions = governanceOptions.Value;
        _logger = logger;
    }

    public async Task<Result<ConversationReply>> GenerateReplyAsync(
        IRequestContext requestContext,
        ConversationGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        var turnResult = await AddTurnAsync(
            requestContext,
            new ConversationTurn(
                request.ThreadId,
                request.UserMessage,
                DateTimeOffset.UtcNow),
            cancellationToken);

        if (turnResult.IsFailure)
        {
            return Result<ConversationReply>.Failure(turnResult.Error!);
        }

        var snapshot = turnResult.Value!;
        var gate = EvaluateAnswerability(request.UserMessage, request.RetrievedContext);
        if (!gate.ShouldGenerate)
        {
            var gateRetrievalConfidence = CalculateRetrievalConfidence(request.RetrievedContext);
            var insufficiencyMessage = BuildInsufficientEvidenceMessage(gate.ReasonCode);
            var abstainAssistantMessage = new ConversationMessage(
                request.ThreadId,
                Role: "assistant",
                Message: insufficiencyMessage,
                OccurredAtUtc: DateTimeOffset.UtcNow);

            var appendInsufficient = await _repository.AppendMessageAsync(requestContext, abstainAssistantMessage, cancellationToken);
            if (appendInsufficient.IsFailure)
            {
                return Result<ConversationReply>.Failure(appendInsufficient.Error!);
            }

            var refreshedInsufficient = await RefreshThreadStateAsync(requestContext, request.ThreadId, cancellationToken: cancellationToken);
            if (refreshedInsufficient.IsFailure)
            {
                return Result<ConversationReply>.Failure(refreshedInsufficient.Error!);
            }

            return Result<ConversationReply>.Success(
                new ConversationReply(
                    ThreadId: request.ThreadId,
                    AssistantMessage: insufficiencyMessage,
                    Snapshot: refreshedInsufficient.Value!,
                    ToolCalls: null,
                    Provider: null,
                    LlmCertainty: null,
                    RetrievalConfidence: gateRetrievalConfidence,
                    OverallConfidence: gateRetrievalConfidence,
                    InsufficiencyReason: gate.ReasonCode,
                    GroundingStatus: gate.ReasonCode == "insufficient_evidence.partial_or_conflicting"
                        ? GroundingStatus.ConflictingEvidence
                        : GroundingStatus.InsufficientEvidence,
                    Diagnostics: new GroundingDiagnostics(
                        RetrievalHitCount: request.RetrievedContext.Count,
                        SelectedEvidenceNodeIds: request.RetrievedContext.Select(static item => item.NodeId).Distinct(StringComparer.Ordinal).ToArray(),
                        CitationValidationResult: "not_applicable",
                        VerifierOutcome: "not_applicable",
                        AbstentionReason: gate.ReasonCode,
                        VerifierRetryCount: 0,
                        PromptInjectionDetected: false)));
        }

        var providerResult = _providerFactory.Resolve(request.Provider);
        if (providerResult.IsFailure)
        {
            return Result<ConversationReply>.Failure(providerResult.Error!);
        }

        var assembled = _promptAssemblyService.Assemble(request, snapshot);
        var completionResult = await CompleteWithGroundedSchemaGuardAsync(
            providerResult.Value!,
            request,
            assembled.Messages,
            cancellationToken);

        if (completionResult.IsFailure)
        {
            return Result<ConversationReply>.Failure(completionResult.Error!);
        }

        var completion = completionResult.Value!;
        var injectionEvaluation = EvaluateRetrievedContentInjectionRisk(request.RetrievedContext, completion.Text);
        if (injectionEvaluation.IsInjectedOutput)
        {
            _logger.LogWarning(
                "Grounding injection detection triggered for thread {ThreadId} with patterns {Patterns}.",
                request.ThreadId,
                string.Join(",", injectionEvaluation.MatchedPatterns));
            completion = completion with
            {
                Text = "I cannot provide this answer because retrieved-content injection signals were detected.",
                GroundedResponse = new GroundedResponseContract(
                    Answer: "I cannot provide this answer because retrieved-content injection signals were detected.",
                    Claims: [],
                    Citations: [],
                    InsufficiencyReason: "prompt_injection_detected",
                    Confidence: null,
                    GroundingStatus: GroundingStatus.ValidationFailed),
                SchemaValidationErrorCode = "prompt_injection_detected"
            };
        }

        var verifierResult = await RunVerifierPassAsync(
            providerResult.Value!,
            request,
            assembled.Messages,
            completion,
            requestContext,
            cancellationToken);
        if (verifierResult.IsFailure)
        {
            return Result<ConversationReply>.Failure(verifierResult.Error!);
        }

        completion = verifierResult.Value!.Completion;
        var retrievalConfidence = CalculateRetrievalConfidence(request.RetrievedContext);
        var normalizedGrounded = NormalizeGroundedResponse(requestContext, completion, request.RetrievedContext);
        var conflictAssessment = AssessConflict(request.RetrievedContext);
        var conflictApplied = ApplyConflictPolicy(normalizedGrounded, conflictAssessment);
        var abstentionDecision = EvaluateAbstentionDecision(conflictApplied.Response, retrievalConfidence, conflictApplied.ForcedReasonCode ?? gate.ReasonCode);
        var finalGrounded = ApplyAbstentionDecision(conflictApplied.Response, abstentionDecision);
        var citationValidationResult = finalGrounded?.GroundingStatus == GroundingStatus.ValidationFailed
            ? (finalGrounded.InsufficiencyReason ?? "validation_failed")
            : "valid";
        if (finalGrounded?.GroundingStatus == GroundingStatus.ValidationFailed)
        {
            _logger.LogWarning(
                "Grounding validation failed for thread {ThreadId} with reason {Reason}.",
                request.ThreadId,
                finalGrounded.InsufficiencyReason);
        }

        var overallConfidence = CalculateOverallConfidence(retrievalConfidence, completion.LlmCertainty);
        var assistantMessage = new ConversationMessage(
            request.ThreadId,
            Role: "assistant",
            Message: finalGrounded?.Answer ?? completion.Text,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        var append = await _repository.AppendMessageAsync(requestContext, assistantMessage, cancellationToken);
        if (append.IsFailure)
        {
            return Result<ConversationReply>.Failure(append.Error!);
        }

        var refreshed = await RefreshThreadStateAsync(requestContext, request.ThreadId, cancellationToken: cancellationToken);
        if (refreshed.IsFailure)
        {
            return Result<ConversationReply>.Failure(refreshed.Error!);
        }

        return Result<ConversationReply>.Success(
            new ConversationReply(
                ThreadId: request.ThreadId,
                AssistantMessage: assistantMessage.Message,
                Snapshot: refreshed.Value!,
                ToolCalls: completion.ToolCalls,
                Provider: completion.Provider,
                LlmCertainty: completion.LlmCertainty,
                RetrievalConfidence: retrievalConfidence,
                OverallConfidence: overallConfidence,
                Claims: finalGrounded?.Claims,
                Citations: finalGrounded?.Citations,
                InsufficiencyReason: finalGrounded?.InsufficiencyReason,
                GroundingStatus: finalGrounded?.GroundingStatus,
                Diagnostics: new GroundingDiagnostics(
                    RetrievalHitCount: request.RetrievedContext.Count,
                    SelectedEvidenceNodeIds: (finalGrounded?.Citations?.Count > 0
                        ? finalGrounded.Citations.Select(static citation => citation.NodeId).Distinct(StringComparer.Ordinal).ToArray()
                        : request.RetrievedContext.Select(static item => item.NodeId).Distinct(StringComparer.Ordinal).ToArray()),
                    CitationValidationResult: citationValidationResult,
                    VerifierOutcome: verifierResult.Value.Outcome switch
                    {
                        VerifierOutcome.Pass => "pass",
                        VerifierOutcome.Revise => "revise",
                        VerifierOutcome.Reject => "reject",
                        _ => "pass"
                    },
                    AbstentionReason: finalGrounded?.InsufficiencyReason,
                    VerifierRetryCount: verifierResult.Value.RetryCount,
                    PromptInjectionDetected: injectionEvaluation.IsInjectedOutput)));
    }

    public async Task<Result<ConversationThreadSnapshot>> AddTurnAsync(
        IRequestContext requestContext,
        ConversationTurn turn,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(turn.ThreadId))
        {
            return Result<ConversationThreadSnapshot>.Failure(
                new Error("conversation.thread_id_required", "ThreadId is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(turn.UserMessage))
        {
            return Result<ConversationThreadSnapshot>.Failure(
                new Error("conversation.user_message_required", "UserMessage is required.", ErrorType.Validation));
        }

        var message = new ConversationMessage(
            turn.ThreadId,
            Role: "user",
            turn.UserMessage,
            turn.OccurredAtUtc,
            turn.ActiveDocumentId,
            turn.ActiveSectionPath);

        var append = await _repository.AppendMessageAsync(requestContext, message, cancellationToken);
        if (append.IsFailure)
        {
            return Result<ConversationThreadSnapshot>.Failure(append.Error!);
        }

        await _stateStore.SetAsync(
            requestContext,
            turn.ThreadId,
            turn.ActiveDocumentId,
            turn.ActiveSectionPath,
            cancellationToken);

        return await RefreshThreadStateAsync(requestContext, turn.ThreadId, cancellationToken: cancellationToken);
    }

    public async Task<Result<ConversationThreadSnapshot>> GetThreadAsync(
        IRequestContext requestContext,
        string threadId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return Result<ConversationThreadSnapshot>.Failure(
                new Error("conversation.thread_id_required", "ThreadId is required.", ErrorType.Validation));
        }

        var messagesResult = await _repository.GetThreadAsync(requestContext, threadId, Math.Max(1, take), cancellationToken);
        if (messagesResult.IsFailure)
        {
            return Result<ConversationThreadSnapshot>.Failure(messagesResult.Error!);
        }

        var persistedStateResult = await _repository.GetThreadStateAsync(requestContext, threadId, cancellationToken);
        if (persistedStateResult.IsFailure)
        {
            return Result<ConversationThreadSnapshot>.Failure(persistedStateResult.Error!);
        }

        var ephemeral = await _stateStore.GetAsync(requestContext, threadId, cancellationToken);

        var messages = messagesResult.Value!;
        var persisted = persistedStateResult.Value;
        var fallbackState = new ConversationThreadState(
            threadId,
            Summary: null,
            ActiveDocumentId: ephemeral?.ActiveDocumentId,
            ActiveSectionPath: ephemeral?.ActiveSectionPath,
            LastRefreshedAtUtc: DateTimeOffset.UtcNow,
            TotalTurns: messages.Count);

        var state = persisted is null
            ? fallbackState
            : persisted with
            {
                ActiveDocumentId = ephemeral?.ActiveDocumentId ?? persisted.ActiveDocumentId,
                ActiveSectionPath = ephemeral?.ActiveSectionPath ?? persisted.ActiveSectionPath,
                TotalTurns = messages.Count
            };

        return Result<ConversationThreadSnapshot>.Success(new ConversationThreadSnapshot(threadId, messages, state));
    }

    public async Task<Result<ConversationThreadSnapshot>> RefreshThreadStateAsync(
        IRequestContext requestContext,
        string threadId,
        int recentWindow = 12,
        CancellationToken cancellationToken = default)
    {
        var snapshotResult = await GetThreadAsync(requestContext, threadId, take: Math.Max(recentWindow, 50), cancellationToken);
        if (snapshotResult.IsFailure)
        {
            return snapshotResult;
        }

        var snapshot = snapshotResult.Value!;
        var recent = snapshot.Messages
            .OrderByDescending(static x => x.OccurredAtUtc)
            .Take(Math.Max(1, recentWindow))
            .OrderBy(static x => x.OccurredAtUtc)
            .ToArray();

        var summary = _summaryBuilder.Build(recent);
        var latest = recent.LastOrDefault();
        var refreshed = new ConversationThreadState(
            ThreadId: threadId,
            Summary: summary,
            ActiveDocumentId: latest?.ActiveDocumentId ?? snapshot.State.ActiveDocumentId,
            ActiveSectionPath: latest?.ActiveSectionPath ?? snapshot.State.ActiveSectionPath,
            LastRefreshedAtUtc: DateTimeOffset.UtcNow,
            TotalTurns: snapshot.Messages.Count);

        var upsert = await _repository.UpsertThreadStateAsync(requestContext, refreshed, cancellationToken);
        if (upsert.IsFailure)
        {
            return Result<ConversationThreadSnapshot>.Failure(upsert.Error!);
        }

        await _stateStore.SetAsync(
            requestContext,
            threadId,
            refreshed.ActiveDocumentId,
            refreshed.ActiveSectionPath,
            cancellationToken);

        return Result<ConversationThreadSnapshot>.Success(snapshot with { State = refreshed });
    }

    private static double? CalculateRetrievalConfidence(IReadOnlyCollection<RetrievedContextItem> context)
    {
        var scored = context
            .Where(static c => c.Score is not null)
            .Select(static c => Math.Clamp(c.Score!.Value, 0d, 1d))
            .ToArray();

        return scored.Length == 0 ? null : scored.Average();
    }

    private static double? CalculateOverallConfidence(double? retrievalConfidence, double? llmCertainty)
    {
        if (retrievalConfidence is null && llmCertainty is null)
        {
            return null;
        }

        if (retrievalConfidence is null)
        {
            return llmCertainty;
        }

        if (llmCertainty is null)
        {
            return retrievalConfidence;
        }

        return Math.Clamp((retrievalConfidence.Value * 0.7) + (llmCertainty.Value * 0.3), 0d, 1d);
    }

    private static AnswerabilityDecision EvaluateAnswerability(
        string userMessage,
        IReadOnlyCollection<RetrievedContextItem> retrievedContext)
    {
        if (retrievedContext.Count == 0)
        {
            return AnswerabilityDecision.Block("insufficient_evidence.retrieval_miss");
        }

        var scores = retrievedContext
            .Select(static item => Math.Clamp(item.Score ?? 0d, 0d, 1d))
            .OrderByDescending(static score => score)
            .ToArray();

        var topScore = scores[0];
        var averageScore = scores.Average();
        var distinctSources = retrievedContext
            .Select(static item => string.IsNullOrWhiteSpace(item.SourceDocumentId) ? item.NodeId : item.SourceDocumentId!)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Count();

        var alignment = CalculateQueryEvidenceAlignment(userMessage, retrievedContext);
        var weakSignalCount = 0;
        if (topScore < MinimumTopScoreThreshold)
        {
            weakSignalCount++;
        }

        if (averageScore < MinimumAverageScoreThreshold)
        {
            weakSignalCount++;
        }

        if (alignment < MinimumAlignmentThreshold)
        {
            weakSignalCount++;
        }

        if (weakSignalCount >= 2)
        {
            return AnswerabilityDecision.Block("insufficient_evidence.retrieval_miss");
        }

        var hasPotentialConflict = distinctSources >= MinimumDistinctSourceCountForStrongEvidence &&
                                   retrievedContext.Select(static item => item.Score ?? 0d).Any(static score => score < 0.30d);

        return hasPotentialConflict
            ? AnswerabilityDecision.Block("insufficient_evidence.partial_or_conflicting")
            : AnswerabilityDecision.Allow();
    }

    private static double CalculateQueryEvidenceAlignment(
        string userMessage,
        IReadOnlyCollection<RetrievedContextItem> retrievedContext)
    {
        var queryTerms = Tokenize(userMessage);
        if (queryTerms.Count == 0)
        {
            return 0d;
        }

        var evidenceTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in retrievedContext)
        {
            foreach (var token in Tokenize(item.Text))
            {
                evidenceTerms.Add(token);
            }
        }

        var overlaps = queryTerms.Count(term => evidenceTerms.Contains(term));
        return overlaps / (double)queryTerms.Count;
    }

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = text
            .Split(QueryTokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length > 2)
            .Select(static token => token.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tokens;
    }

    private static string BuildInsufficientEvidenceMessage(string reasonCode)
        => reasonCode switch
        {
            "insufficient_evidence.retrieval_miss" => "I do not have enough grounded evidence to answer this yet. Please refine the query or provide additional source context.",
            "insufficient_evidence.partial_or_conflicting" => "Available evidence is partial or conflicting, so I cannot provide a grounded answer yet.",
            _ => "I do not have enough grounded evidence to answer this yet."
        };

    private readonly record struct AnswerabilityDecision(bool ShouldGenerate, string ReasonCode)
    {
        public static AnswerabilityDecision Allow() => new(true, string.Empty);

        public static AnswerabilityDecision Block(string reasonCode) => new(false, reasonCode);
    }

    private async Task<Result<LlmCompletionResponse>> CompleteWithGroundedSchemaGuardAsync(
        ILlmProvider provider,
        ConversationGenerateRequest request,
        IReadOnlyCollection<LlmMessage> baseMessages,
        CancellationToken cancellationToken)
    {
        if (request.PolicyMode != GenerationPolicyMode.Grounded)
        {
            return await provider.CompleteAsync(new LlmCompletionRequest(baseMessages), cancellationToken);
        }

        var messages = baseMessages.ToList();
        for (var attempt = 0; attempt <= GroundedSchemaRetryLimit; attempt++)
        {
            var response = await provider.CompleteAsync(new LlmCompletionRequest(messages), cancellationToken);
            if (response.IsFailure)
            {
                return response;
            }

            if (response.Value!.GroundedResponse is not null)
            {
                return response;
            }

            if (attempt == GroundedSchemaRetryLimit)
            {
                return Result<LlmCompletionResponse>.Success(
                    new LlmCompletionResponse(
                        Text: "I cannot return a grounded answer right now because the response did not meet validation requirements.",
                        ToolCalls: [],
                        Usage: response.Value.Usage,
                        Provider: response.Value.Provider,
                        LlmCertainty: null,
                        GroundedResponse: new GroundedResponseContract(
                            Answer: "I cannot return a grounded answer right now because the response did not meet validation requirements.",
                            Claims: [],
                            Citations: [],
                            InsufficiencyReason: response.Value.SchemaValidationErrorCode ?? "schema_invalid",
                            Confidence: null,
                            GroundingStatus: GroundingStatus.ValidationFailed),
                        SchemaValidationErrorCode: response.Value.SchemaValidationErrorCode ?? "schema_invalid"));
            }

            messages.Add(new LlmMessage(
                "system",
                "Your previous response failed schema validation. Return valid grounded schema with answer, claims, citations, insufficiency_reason, confidence, grounding_status."));
        }

        return Result<LlmCompletionResponse>.Failure(new Error("conversation.schema_guard_failed", "Schema validation guard failed.", ErrorType.Unexpected));
    }

    private static GroundedResponseContract? NormalizeGroundedResponse(
        IRequestContext requestContext,
        LlmCompletionResponse completion,
        IReadOnlyCollection<RetrievedContextItem> retrievedContext)
    {
        if (completion.GroundedResponse is null)
        {
            return null;
        }

        var citationValidation = ValidateCitations(requestContext, completion.GroundedResponse, retrievedContext);
        if (!citationValidation.IsValid)
        {
            return new GroundedResponseContract(
                Answer: "I cannot return a grounded answer right now because citation span validation failed.",
                Claims: [],
                Citations: [],
                InsufficiencyReason: citationValidation.ErrorCode,
                Confidence: null,
                GroundingStatus: GroundingStatus.ValidationFailed);
        }

        var normalizedConfidence = completion.GroundedResponse.Confidence ?? completion.LlmCertainty;
        return completion.GroundedResponse with
        {
            Answer = string.IsNullOrWhiteSpace(completion.GroundedResponse.Answer) ? completion.Text : completion.GroundedResponse.Answer,
            Confidence = normalizedConfidence
        };
    }

    private static CitationValidationResult ValidateCitations(
        IRequestContext requestContext,
        GroundedResponseContract grounded,
        IReadOnlyCollection<RetrievedContextItem> retrievedContext)
    {
        var byNode = retrievedContext
            .GroupBy(static item => item.NodeId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var citationMap = grounded.Citations.ToDictionary(static citation => citation.CitationId, StringComparer.Ordinal);

        foreach (var citation in grounded.Citations)
        {
            if (!byNode.TryGetValue(citation.NodeId, out var matchingContext))
            {
                return CitationValidationResult.Invalid("citation_invalid.node_not_in_retrieved_context");
            }

            if (!ValidateScopeIntegrity(requestContext, citation, matchingContext))
            {
                return CitationValidationResult.Invalid("citation_invalid.scope_mismatch");
            }

            if (!ValidateAclIntegrity(requestContext, matchingContext))
            {
                return CitationValidationResult.Invalid("citation_invalid.acl_violation");
            }

            var requiresSpan = matchingContext.Any(static item =>
                !string.IsNullOrWhiteSpace(item.SpanId) || item.StartOffset is not null || item.EndOffset is not null);
            if (!requiresSpan)
            {
                continue;
            }

            var spanProvided = !string.IsNullOrWhiteSpace(citation.SpanId) ||
                               (citation.StartOffset is not null && citation.EndOffset is not null);
            if (!spanProvided)
            {
                return CitationValidationResult.Invalid("citation_invalid.span_required_missing");
            }

            var spanMatched = matchingContext.Any(item =>
                (!string.IsNullOrWhiteSpace(item.SpanId) && string.Equals(item.SpanId, citation.SpanId, StringComparison.Ordinal)) ||
                (item.StartOffset == citation.StartOffset && item.EndOffset == citation.EndOffset));
            if (!spanMatched)
            {
                return CitationValidationResult.Invalid("citation_invalid.span_mismatch");
            }
        }

        foreach (var claim in grounded.Claims)
        {
            foreach (var citationId in claim.CitationIds)
            {
                if (!citationMap.TryGetValue(citationId, out var citation))
                {
                    return CitationValidationResult.Invalid("citation_invalid.claim_reference_missing");
                }

                if (!byNode.TryGetValue(citation.NodeId, out var contexts))
                {
                    return CitationValidationResult.Invalid("citation_invalid.node_not_in_retrieved_context");
                }

                var contextSpanText = ResolveSpanText(citation, contexts);
                if (string.IsNullOrWhiteSpace(contextSpanText))
                {
                    return CitationValidationResult.Invalid("citation_invalid.span_not_resolved");
                }

                if (!HasLexicalSupport(claim.Text, contextSpanText))
                {
                    return CitationValidationResult.Invalid("citation_invalid.claim_span_unsupported");
                }
            }
        }

        return CitationValidationResult.Valid();
    }

    private static bool ValidateScopeIntegrity(
        IRequestContext requestContext,
        GroundedCitation citation,
        IReadOnlyCollection<RetrievedContextItem> contexts)
    {
        if (!string.IsNullOrWhiteSpace(citation.TenantId) &&
            !string.Equals(citation.TenantId, requestContext.TenantId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(citation.AppId) &&
            !string.Equals(citation.AppId, requestContext.AppId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(citation.CollectionId) &&
            !string.Equals(citation.CollectionId, requestContext.CollectionId, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var context in contexts)
        {
            if (!string.IsNullOrWhiteSpace(context.TenantId) &&
                !string.Equals(context.TenantId, requestContext.TenantId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(context.AppId) &&
                !string.Equals(context.AppId, requestContext.AppId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(context.CollectionId) &&
                !string.Equals(context.CollectionId, requestContext.CollectionId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateAclIntegrity(IRequestContext requestContext, IReadOnlyCollection<RetrievedContextItem> contexts)
    {
        var allowed = requestContext.AllowedDocumentGroups
            .Where(static g => !string.IsNullOrWhiteSpace(g))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var context in contexts)
        {
            var scopes = context.AclScopes?
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            if (scopes is null || scopes.Length == 0)
            {
                return false;
            }

            if (scopes.Contains("public", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (allowed.Count == 0)
            {
                return false;
            }

            var overlap = scopes.Any(scope => allowed.Contains(scope));
            if (!overlap)
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolveSpanText(GroundedCitation citation, IReadOnlyCollection<RetrievedContextItem> contexts)
    {
        var bySpanId = contexts.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.SpanId) &&
            !string.IsNullOrWhiteSpace(citation.SpanId) &&
            string.Equals(item.SpanId, citation.SpanId, StringComparison.Ordinal));
        if (bySpanId is not null)
        {
            return SliceSpan(bySpanId.Text, citation.StartOffset ?? bySpanId.StartOffset, citation.EndOffset ?? bySpanId.EndOffset);
        }

        var byOffsets = contexts.FirstOrDefault(item =>
            item.StartOffset == citation.StartOffset &&
            item.EndOffset == citation.EndOffset);
        if (byOffsets is not null)
        {
            return SliceSpan(byOffsets.Text, citation.StartOffset, citation.EndOffset);
        }

        return string.Empty;
    }

    private static string SliceSpan(string text, int? startOffset, int? endOffset)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (startOffset is null || endOffset is null)
        {
            return text;
        }

        var start = Math.Clamp(startOffset.Value, 0, text.Length);
        var end = Math.Clamp(endOffset.Value, start, text.Length);
        if (end <= start)
        {
            return string.Empty;
        }

        return text.Substring(start, end - start);
    }

    private static bool HasLexicalSupport(string claimText, string spanText)
    {
        var claimTokens = Tokenize(claimText);
        if (claimTokens.Count == 0)
        {
            return false;
        }

        var spanTokens = Tokenize(spanText);
        if (spanTokens.Count == 0)
        {
            return false;
        }

        var overlap = claimTokens.Count(token => spanTokens.Contains(token));
        var overlapRatio = overlap / (double)claimTokens.Count;
        return overlapRatio >= 0.25d;
    }

    private static InjectionEvaluation EvaluateRetrievedContentInjectionRisk(
        IReadOnlyCollection<RetrievedContextItem> retrievedContext,
        string completionText)
    {
        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var context in retrievedContext)
        {
            var sanitized = RetrievedContentSanitizer.Sanitize(context.Text);
            foreach (var pattern in sanitized.MatchedPatterns)
            {
                patterns.Add(pattern);
            }
        }

        if (patterns.Count == 0)
        {
            return InjectionEvaluation.Safe();
        }

        var loweredCompletion = completionText.ToLowerInvariant();
        var executed = patterns.Any(pattern => loweredCompletion.Contains(pattern, StringComparison.Ordinal));
        return executed
            ? InjectionEvaluation.Injected(patterns.ToArray())
            : InjectionEvaluation.Safe();
    }

    private ConflictAssessment AssessConflict(IReadOnlyCollection<RetrievedContextItem> retrievedContext)
    {
        if (retrievedContext.Count < 2)
        {
            return ConflictAssessment.None();
        }

        var conflicting = new List<RetrievedContextItem>();
        var ordered = retrievedContext.Where(static item => !string.IsNullOrWhiteSpace(item.Text)).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            for (var j = i + 1; j < ordered.Length; j++)
            {
                if (AreTextsConflicting(ordered[i].Text, ordered[j].Text))
                {
                    conflicting.Add(ordered[i]);
                    conflicting.Add(ordered[j]);
                }
            }
        }

        if (conflicting.Count == 0)
        {
            return ConflictAssessment.None();
        }

        var candidates = conflicting
            .DistinctBy(static item => item.NodeId, StringComparer.Ordinal)
            .ToArray();

        RetrievedContextItem? preferred = _governanceOptions.ConflictPolicy switch
        {
            ConflictResolutionPolicy.PreferNewest => candidates
                .OrderByDescending(static item => item.SourceUpdatedAtUtc ?? DateTimeOffset.MinValue)
                .ThenByDescending(static item => item.Score ?? 0d)
                .FirstOrDefault(),
            ConflictResolutionPolicy.PreferHighestAuthority => candidates
                .OrderByDescending(static item => item.SourceAuthorityScore ?? 0d)
                .ThenByDescending(static item => item.Score ?? 0d)
                .FirstOrDefault(),
            _ => null
        };

        return new ConflictAssessment(true, candidates, preferred);
    }

    private static bool AreTextsConflicting(string leftText, string rightText)
    {
        var left = leftText.ToLowerInvariant();
        var right = rightText.ToLowerInvariant();

        static bool IsNegative(string value)
            => value.Contains(" not ", StringComparison.Ordinal) ||
               value.Contains(" no ", StringComparison.Ordinal) ||
               value.Contains("unknown", StringComparison.Ordinal) ||
               value.Contains("cannot", StringComparison.Ordinal);

        var leftNegative = IsNegative(left);
        var rightNegative = IsNegative(right);
        if (leftNegative != rightNegative)
        {
            return true;
        }

        var leftNumbers = ExtractNumbers(left);
        var rightNumbers = ExtractNumbers(right);
        return leftNumbers.Count > 0 &&
               rightNumbers.Count > 0 &&
               !leftNumbers.SetEquals(rightNumbers);
    }

    private static HashSet<string> ExtractNumbers(string text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var current = new List<char>();
        foreach (var ch in text)
        {
            if (char.IsDigit(ch))
            {
                current.Add(ch);
                continue;
            }

            if (current.Count > 0)
            {
                result.Add(new string(current.ToArray()));
                current.Clear();
            }
        }

        if (current.Count > 0)
        {
            result.Add(new string(current.ToArray()));
        }

        return result;
    }

    private ConflictPolicyResult ApplyConflictPolicy(GroundedResponseContract? grounded, ConflictAssessment assessment)
    {
        if (!assessment.HasConflict)
        {
            return ConflictPolicyResult.None(grounded);
        }

        return _governanceOptions.ConflictPolicy switch
        {
            ConflictResolutionPolicy.Abstain => ConflictPolicyResult.Override(
                new GroundedResponseContract(
                    Answer: "Retrieved sources conflict, so I cannot provide a grounded answer.",
                    Claims: [],
                    Citations: [],
                    InsufficiencyReason: "conflicting_evidence.detected",
                    Confidence: null,
                    GroundingStatus: GroundingStatus.ConflictingEvidence),
                "insufficient_evidence.partial_or_conflicting"),
            ConflictResolutionPolicy.SummarizeDisagreement => ConflictPolicyResult.Override(
                BuildSummarizedConflictResponse(grounded),
                "insufficient_evidence.partial_or_conflicting"),
            ConflictResolutionPolicy.PreferNewest => ConflictPolicyResult.Override(
                ApplyPreferredSource(grounded, assessment.PreferredNodeId, "conflicting_evidence.preferred_newest"),
                "insufficient_evidence.partial_or_conflicting"),
            ConflictResolutionPolicy.PreferHighestAuthority => ConflictPolicyResult.Override(
                ApplyPreferredSource(grounded, assessment.PreferredNodeId, "conflicting_evidence.preferred_authority"),
                "insufficient_evidence.partial_or_conflicting"),
            _ => ConflictPolicyResult.None(grounded)
        };
    }

    private static GroundedResponseContract BuildSummarizedConflictResponse(GroundedResponseContract? grounded)
    {
        if (grounded is null)
        {
            return new GroundedResponseContract(
                Answer: "Retrieved sources disagree. Unable to provide a fully grounded answer.",
                Claims: [],
                Citations: [],
                InsufficiencyReason: "conflicting_evidence.detected",
                Confidence: null,
                GroundingStatus: GroundingStatus.ConflictingEvidence);
        }

        return grounded with
        {
            Answer = "Retrieved sources disagree: " + grounded.Answer,
            InsufficiencyReason = "conflicting_evidence.detected",
            GroundingStatus = GroundingStatus.ConflictingEvidence
        };
    }

    private static GroundedResponseContract ApplyPreferredSource(
        GroundedResponseContract? grounded,
        string? preferredNodeId,
        string reasonCode)
    {
        if (grounded is null || string.IsNullOrWhiteSpace(preferredNodeId))
        {
            return BuildSummarizedConflictResponse(grounded);
        }

        var preferredCitations = grounded.Citations
            .Where(citation => string.Equals(citation.NodeId, preferredNodeId, StringComparison.Ordinal))
            .ToArray();
        if (preferredCitations.Length == 0)
        {
            return BuildSummarizedConflictResponse(grounded);
        }

        var preferredCitationIds = preferredCitations.Select(static citation => citation.CitationId).ToHashSet(StringComparer.Ordinal);
        var preferredClaims = grounded.Claims
            .Where(claim => claim.CitationIds.Any(preferredCitationIds.Contains))
            .Select(claim => claim with
            {
                CitationIds = claim.CitationIds.Where(preferredCitationIds.Contains).ToArray()
            })
            .Where(static claim => claim.CitationIds.Count > 0)
            .ToArray();

        if (preferredClaims.Length == 0)
        {
            return BuildSummarizedConflictResponse(grounded);
        }

        return grounded with
        {
            Claims = preferredClaims,
            Citations = preferredCitations,
            GroundingStatus = GroundingStatus.PartiallyGrounded,
            InsufficiencyReason = reasonCode
        };
    }

    private readonly record struct CitationValidationResult(bool IsValid, string? ErrorCode)
    {
        public static CitationValidationResult Valid() => new(true, null);

        public static CitationValidationResult Invalid(string errorCode) => new(false, errorCode);
    }

    private AbstentionDecision EvaluateAbstentionDecision(
        GroundedResponseContract? grounded,
        double? retrievalConfidence,
        string gateReasonCode)
    {
        if (grounded is null)
        {
            return AbstentionDecision.None();
        }

        if (grounded.GroundingStatus == GroundingStatus.ValidationFailed)
        {
            return AbstentionDecision.None();
        }

        if (grounded.GroundingStatus == GroundingStatus.ConflictingEvidence)
        {
            return AbstentionDecision.None();
        }

        var contradictionSignal = string.Equals(gateReasonCode, "insufficient_evidence.partial_or_conflicting", StringComparison.Ordinal) ? 1d : 0d;
        if (contradictionSignal >= 1d)
        {
            return _governanceOptions.AllowPartialAnswer
                ? AbstentionDecision.Partial("insufficient_evidence.partial_or_conflicting")
                : AbstentionDecision.Abstain("insufficient_evidence.partial_or_conflicting");
        }

        var confidence = retrievalConfidence ?? 0d;
        var coverage = ComputeEvidenceCoverage(grounded);
        var citationCompleteness = ComputeCitationCompleteness(grounded);
        var belowConfidence = confidence < _governanceOptions.MinimumRetrievalConfidence;
        var belowCoverage = coverage < _governanceOptions.MinimumEvidenceCoverage;
        var incompleteCitations = _governanceOptions.RequireCitationCompleteness && citationCompleteness < 1d;

        if (!belowConfidence && !belowCoverage && !incompleteCitations)
        {
            return AbstentionDecision.None();
        }

        if (_governanceOptions.AllowPartialAnswer && coverage >= _governanceOptions.MinimumCoverageForPartialAnswer && grounded.Claims.Count > 0)
        {
            return AbstentionDecision.Partial("insufficient_evidence.partial");
        }

        return AbstentionDecision.Abstain("insufficient_evidence.threshold");
    }

    private static GroundedResponseContract? ApplyAbstentionDecision(
        GroundedResponseContract? grounded,
        AbstentionDecision decision)
    {
        if (grounded is null || decision.Kind == AbstentionKind.None)
        {
            return grounded;
        }

        if (decision.Kind == AbstentionKind.Partial)
        {
            return grounded with
            {
                GroundingStatus = GroundingStatus.PartiallyGrounded,
                InsufficiencyReason = decision.ReasonCode ?? grounded.InsufficiencyReason
            };
        }

        return new GroundedResponseContract(
            Answer: "I cannot provide a fully grounded answer with the current evidence.",
            Claims: [],
            Citations: [],
            InsufficiencyReason: decision.ReasonCode ?? "insufficient_evidence.threshold",
            Confidence: null,
            GroundingStatus: GroundingStatus.InsufficientEvidence);
    }

    private static double ComputeEvidenceCoverage(GroundedResponseContract grounded)
    {
        if (grounded.Claims.Count == 0)
        {
            return 0d;
        }

        var supportedClaims = grounded.Claims.Count(static claim => claim.CitationIds.Count > 0);
        return supportedClaims / (double)grounded.Claims.Count;
    }

    private static double ComputeCitationCompleteness(GroundedResponseContract grounded)
    {
        var claimCitationCount = grounded.Claims.SelectMany(static claim => claim.CitationIds).Distinct(StringComparer.Ordinal).Count();
        if (claimCitationCount == 0)
        {
            return 0d;
        }

        var providedCitationIds = grounded.Citations.Select(static citation => citation.CitationId).ToHashSet(StringComparer.Ordinal);
        var linked = grounded.Claims
            .SelectMany(static claim => claim.CitationIds)
            .Distinct(StringComparer.Ordinal)
            .Count(providedCitationIds.Contains);
        return linked / (double)claimCitationCount;
    }

    private readonly record struct AbstentionDecision(AbstentionKind Kind, string? ReasonCode)
    {
        public static AbstentionDecision None() => new(AbstentionKind.None, null);

        public static AbstentionDecision Partial(string reasonCode) => new(AbstentionKind.Partial, reasonCode);

        public static AbstentionDecision Abstain(string reasonCode) => new(AbstentionKind.Abstain, reasonCode);
    }

    private enum AbstentionKind
    {
        None = 0,
        Partial = 1,
        Abstain = 2
    }

    private readonly record struct ConflictAssessment(
        bool HasConflict,
        IReadOnlyCollection<RetrievedContextItem> Candidates,
        RetrievedContextItem? Preferred)
    {
        public string? PreferredNodeId => Preferred?.NodeId;

        public static ConflictAssessment None() => new(false, [], null);
    }

    private readonly record struct ConflictPolicyResult(
        GroundedResponseContract? Response,
        string? ForcedReasonCode)
    {
        public static ConflictPolicyResult None(GroundedResponseContract? response) => new(response, null);

        public static ConflictPolicyResult Override(GroundedResponseContract response, string forcedReasonCode) => new(response, forcedReasonCode);
    }

    private readonly record struct InjectionEvaluation(
        bool IsInjectedOutput,
        IReadOnlyCollection<string> MatchedPatterns)
    {
        public static InjectionEvaluation Safe() => new(false, []);

        public static InjectionEvaluation Injected(IReadOnlyCollection<string> patterns) => new(true, patterns);
    }

    private async Task<Result<VerifierResult>> RunVerifierPassAsync(
        ILlmProvider provider,
        ConversationGenerateRequest request,
        IReadOnlyCollection<LlmMessage> baseMessages,
        LlmCompletionResponse initialCompletion,
        IRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        if (request.PolicyMode != GenerationPolicyMode.Grounded || !_governanceOptions.EnableVerifierPass)
        {
            return Result<VerifierResult>.Success(new VerifierResult(initialCompletion, VerifierOutcome.Pass, 0));
        }

        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        var completion = initialCompletion;
        var sawRevise = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = NormalizeGroundedResponse(requestContext, completion, request.RetrievedContext);
            var outcome = EvaluateVerifierOutcome(normalized);
            if (outcome == VerifierOutcome.Pass)
            {
                var finalOutcome = sawRevise ? VerifierOutcome.Revise : VerifierOutcome.Pass;
                return Result<VerifierResult>.Success(new VerifierResult(completion, finalOutcome, attempts));
            }

            if (outcome == VerifierOutcome.Reject)
            {
                return Result<VerifierResult>.Success(new VerifierResult(
                    BuildVerifierRejectCompletion(completion),
                    VerifierOutcome.Reject,
                    attempts));
            }

            var maxAttempts = Math.Max(0, _governanceOptions.VerifierMaxAttempts);
            var maxElapsed = Math.Max(50, _governanceOptions.VerifierMaxElapsedMs);
            if (attempts >= maxAttempts || stopwatch.ElapsedMilliseconds >= maxElapsed)
            {
                return Result<VerifierResult>.Success(new VerifierResult(
                    BuildVerifierRejectCompletion(completion),
                    VerifierOutcome.Reject,
                    attempts));
            }

            attempts++;
            sawRevise = true;
            var revisedMessages = baseMessages.ToList();
            revisedMessages.Add(new LlmMessage(
                "system",
                "Verifier outcome=revise. Regenerate with stricter claim-to-citation grounding and valid schema only."));

            var revised = await CompleteWithGroundedSchemaGuardAsync(provider, request, revisedMessages, cancellationToken);
            if (revised.IsFailure)
            {
                return Result<VerifierResult>.Failure(revised.Error!);
            }

            completion = revised.Value!;
        }
    }

    private static VerifierOutcome EvaluateVerifierOutcome(GroundedResponseContract? grounded)
    {
        if (grounded is null)
        {
            return VerifierOutcome.Revise;
        }

        return grounded.GroundingStatus switch
        {
            GroundingStatus.ValidationFailed => VerifierOutcome.Revise,
            GroundingStatus.Grounded or GroundingStatus.PartiallyGrounded or GroundingStatus.ConflictingEvidence or GroundingStatus.InsufficientEvidence => VerifierOutcome.Pass,
            _ => VerifierOutcome.Reject
        };
    }

    private static LlmCompletionResponse BuildVerifierRejectCompletion(LlmCompletionResponse original)
    {
        var grounded = new GroundedResponseContract(
            Answer: "I cannot provide a grounded answer because verifier checks failed.",
            Claims: [],
            Citations: [],
            InsufficiencyReason: "verifier_reject",
            Confidence: null,
            GroundingStatus: GroundingStatus.ValidationFailed);
        return original with
        {
            Text = grounded.Answer,
            GroundedResponse = grounded,
            SchemaValidationErrorCode = "verifier_reject"
        };
    }

    private readonly record struct VerifierResult(
        LlmCompletionResponse Completion,
        VerifierOutcome Outcome,
        int RetryCount);

    private enum VerifierOutcome
    {
        Pass = 0,
        Revise = 1,
        Reject = 2
    }
}
