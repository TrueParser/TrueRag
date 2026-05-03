using TrueRag.Conversations;
using TrueRag.Conversations.Llm;
using TrueRag.Conversations.PromptAssembly;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.UnitTests.Conversations;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task AddTurnAsync_PersistsTurn_RefreshesSummary_AndTracksScope()
    {
        var repository = new InMemoryConversationRepository();
        var stateStore = new InMemoryStateStore();
        var summary = new ConversationSummaryBuilder();
        var service = BuildService(repository, stateStore, summary);
        var ctx = new RequestContext("tenant-1", "app-1", "user-1", ["reader"], ["g1"]);

        var result = await service.AddTurnAsync(
            ctx,
            new ConversationTurn("thread-1", "What changed in section 4?", DateTimeOffset.UtcNow, "doc-77", "Document/Section4"));

        Assert.True(result.IsSuccess);
        Assert.Equal("thread-1", result.Value!.ThreadId);
        Assert.Single(result.Value.Messages);
        Assert.Equal("doc-77", result.Value.State.ActiveDocumentId);
        Assert.Equal("Document/Section4", result.Value.State.ActiveSectionPath);
        Assert.Contains("user:", result.Value.State.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshThreadStateAsync_UsesRecentWindow_ForSummary()
    {
        var repository = new InMemoryConversationRepository();
        var stateStore = new InMemoryStateStore();
        var summary = new ConversationSummaryBuilder();
        var service = BuildService(repository, stateStore, summary);
        var ctx = new RequestContext("tenant-1", "app-1", "user-1", ["reader"], ["g1"]);

        for (var i = 0; i < 6; i++)
        {
            await repository.AppendMessageAsync(
                ctx,
                new ConversationMessage("thread-2", "user", $"message-{i}", DateTimeOffset.UtcNow.AddMinutes(i)));
        }

        var refreshed = await service.RefreshThreadStateAsync(ctx, "thread-2", recentWindow: 3);

        Assert.True(refreshed.IsSuccess);
        Assert.Contains("message-5", refreshed.Value!.State.Summary);
        Assert.DoesNotContain("message-1", refreshed.Value.State.Summary ?? string.Empty);
    }

    [Fact]
    public async Task GenerateReplyAsync_UsesSelectedProvider_AndPersistsAssistantMessage()
    {
        var repository = new InMemoryConversationRepository();
        var stateStore = new InMemoryStateStore();
        var summary = new ConversationSummaryBuilder();
        var service = BuildService(repository, stateStore, summary);
        var ctx = new RequestContext("tenant-1", "app-1", "user-1", ["reader"], ["g1"]);

        var result = await service.GenerateReplyAsync(
            ctx,
            new ConversationGenerateRequest(
                ThreadId: "thread-3",
                UserMessage: "Find related citation",
                RetrievedContext:
                [
                    new RetrievedContextItem("n-1", "The contract clause is in section 8", "doc-1", 0.82)
                ],
                Provider: "openai",
                PromptTokenBudget: 1200));

        Assert.True(result.IsSuccess);
        Assert.Equal("openai", result.Value!.Provider);
        Assert.NotEmpty(result.Value.AssistantMessage);
        Assert.NotNull(result.Value.ToolCalls);
        Assert.NotEmpty(result.Value.ToolCalls!);
        Assert.NotNull(result.Value.RetrievalConfidence);
        Assert.NotNull(result.Value.OverallConfidence);

        var thread = await repository.GetThreadAsync(ctx, "thread-3", 50);
        Assert.True(thread.IsSuccess);
        Assert.Equal(2, thread.Value!.Count); // user + assistant
    }

    private static ConversationService BuildService(
        IConversationRepository repository,
        IConversationStateStore stateStore,
        IConversationSummaryBuilder summaryBuilder)
    {
        var prompt = new TestPromptAssemblyService();
        var parser = new LlmResponseParser();
        var providers = new ILlmProvider[]
        {
            new LocalLlmProvider(parser),
            new OpenAiLlmProvider(parser),
            new AnthropicLlmProvider(parser)
        };
        var factory = new TestLlmProviderFactory(providers, "local");
        return new ConversationService(repository, stateStore, summaryBuilder, prompt, factory);
    }

    private sealed class TestPromptAssemblyService : IPromptAssemblyService
    {
        public PromptAssemblyResult Assemble(ConversationGenerateRequest request, ConversationThreadSnapshot snapshot)
        {
            return new PromptAssemblyResult(
                [
                    new LlmMessage("system", "system-instruction"),
                    new LlmMessage("user", request.UserMessage)
                ],
                EstimatedPromptTokens: 10,
                BudgetUsed: 10,
                BudgetTotal: request.PromptTokenBudget ?? 1000);
        }
    }

    private sealed class TestLlmProviderFactory : ILlmProviderFactory
    {
        private readonly IReadOnlyDictionary<string, ILlmProvider> _providers;
        private readonly string _defaultProvider;

        public TestLlmProviderFactory(IEnumerable<ILlmProvider> providers, string defaultProvider)
        {
            _providers = providers.ToDictionary(static provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase);
            _defaultProvider = defaultProvider;
        }

        public Result<ILlmProvider> Resolve(string? providerId)
        {
            var key = string.IsNullOrWhiteSpace(providerId) ? _defaultProvider : providerId!;
            if (_providers.TryGetValue(key, out var provider))
            {
                return Result<ILlmProvider>.Success(provider);
            }

            return Result<ILlmProvider>.Failure(new Error("provider.not_found", key, ErrorType.Validation));
        }
    }

    private sealed class InMemoryStateStore : IConversationStateStore
    {
        private readonly Dictionary<string, ConversationEphemeralState> _items = new(StringComparer.Ordinal);

        public Task SetAsync(IRequestContext requestContext, string threadId, string? activeDocumentId, string? activeSectionPath, CancellationToken cancellationToken = default)
        {
            _items[requestContext.TenantId + ":" + requestContext.AppId + ":" + threadId] =
                new ConversationEphemeralState(activeDocumentId, activeSectionPath, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }

        public Task<ConversationEphemeralState?> GetAsync(IRequestContext requestContext, string threadId, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(requestContext.TenantId + ":" + requestContext.AppId + ":" + threadId, out var value);
            return Task.FromResult(value);
        }
    }

    private sealed class InMemoryConversationRepository : IConversationRepository
    {
        private readonly Dictionary<string, List<ConversationMessage>> _messages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ConversationThreadState> _states = new(StringComparer.Ordinal);

        public Task<Result> AppendMessageAsync(IRequestContext requestContext, ConversationMessage message, CancellationToken cancellationToken = default)
        {
            var key = ScopeKey(requestContext, message.ThreadId);
            if (!_messages.TryGetValue(key, out var list))
            {
                list = [];
                _messages[key] = list;
            }

            list.Add(message);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<IReadOnlyCollection<ConversationMessage>>> GetThreadAsync(IRequestContext requestContext, string threadId, int take, CancellationToken cancellationToken = default)
        {
            var key = ScopeKey(requestContext, threadId);
            _messages.TryGetValue(key, out var list);
            IReadOnlyCollection<ConversationMessage> result = list?.TakeLast(Math.Max(1, take)).ToArray() ?? [];
            return Task.FromResult(Result<IReadOnlyCollection<ConversationMessage>>.Success(result));
        }

        public Task<Result<ConversationThreadState?>> GetThreadStateAsync(IRequestContext requestContext, string threadId, CancellationToken cancellationToken = default)
        {
            _states.TryGetValue(ScopeKey(requestContext, threadId), out var value);
            return Task.FromResult(Result<ConversationThreadState?>.Success(value));
        }

        public Task<Result> UpsertThreadStateAsync(IRequestContext requestContext, ConversationThreadState state, CancellationToken cancellationToken = default)
        {
            _states[ScopeKey(requestContext, state.ThreadId)] = state;
            return Task.FromResult(Result.Success());
        }

        private static string ScopeKey(IRequestContext ctx, string threadId)
            => ctx.TenantId + ":" + ctx.AppId + ":" + threadId;
    }
}
