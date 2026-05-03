using TrueRag.Conversations.PromptAssembly;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Conversations;

internal sealed class ConversationService : IConversationService
{
    private readonly IConversationRepository _repository;
    private readonly IConversationStateStore _stateStore;
    private readonly IConversationSummaryBuilder _summaryBuilder;
    private readonly IPromptAssemblyService _promptAssemblyService;
    private readonly ILlmProviderFactory _providerFactory;

    public ConversationService(
        IConversationRepository repository,
        IConversationStateStore stateStore,
        IConversationSummaryBuilder summaryBuilder,
        IPromptAssemblyService promptAssemblyService,
        ILlmProviderFactory providerFactory)
    {
        _repository = repository;
        _stateStore = stateStore;
        _summaryBuilder = summaryBuilder;
        _promptAssemblyService = promptAssemblyService;
        _providerFactory = providerFactory;
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
        var providerResult = _providerFactory.Resolve(request.Provider);
        if (providerResult.IsFailure)
        {
            return Result<ConversationReply>.Failure(providerResult.Error!);
        }

        var assembled = _promptAssemblyService.Assemble(request, snapshot);
        var completionResult = await providerResult.Value!.CompleteAsync(
            new LlmCompletionRequest(assembled.Messages),
            cancellationToken);

        if (completionResult.IsFailure)
        {
            return Result<ConversationReply>.Failure(completionResult.Error!);
        }

        var completion = completionResult.Value!;
        var retrievalConfidence = CalculateRetrievalConfidence(request.RetrievedContext);
        var overallConfidence = CalculateOverallConfidence(retrievalConfidence, completion.LlmCertainty);
        var assistantMessage = new ConversationMessage(
            request.ThreadId,
            Role: "assistant",
            Message: completion.Text,
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
                AssistantMessage: completion.Text,
                Snapshot: refreshed.Value!,
                ToolCalls: completion.ToolCalls,
                Provider: completion.Provider,
                LlmCertainty: completion.LlmCertainty,
                RetrievalConfidence: retrievalConfidence,
                OverallConfidence: overallConfidence));
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
}
