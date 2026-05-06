using TrueRag.Api.Contracts;
using TrueRag.Api.Services;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.UnitTests.Api;

public sealed class ConversationApiServiceTests
{
    [Fact]
    public async Task Generate_WhenPolicyModeIsUtility_ReturnsValidationError()
    {
        var fakeConversation = new FakeConversationService();
        var service = new ConversationApiService(fakeConversation);
        var context = new RequestContext("tenant-1", "app-1", "user-1", ["reader"], ["g1"], "collection-1");

        var result = await service.Generate(
            context,
            new RagGenerateInput(
                ThreadId: "thread-1",
                UserMessage: "hello",
                RetrievedContext: [],
                Provider: "openai",
                PromptTokenBudget: 1024,
                PolicyMode: GenerationPolicyMode.Utility));

        Assert.True(result.IsFailure);
        Assert.Equal("conversation.grounded_route_requires_grounded_mode", result.Error?.Code);
        Assert.Equal(0, fakeConversation.GenerateCalls);
    }

    [Fact]
    public async Task Generate_WhenPolicyModeMissing_DefaultsToGrounded()
    {
        var fakeConversation = new FakeConversationService();
        var service = new ConversationApiService(fakeConversation);
        var context = new RequestContext("tenant-1", "app-1", "user-1", ["reader"], ["g1"], "collection-1");

        var result = await service.Generate(
            context,
            new RagGenerateInput(
                ThreadId: "thread-1",
                UserMessage: "hello",
                RetrievedContext: [],
                Provider: "openai",
                PromptTokenBudget: 1024,
                PolicyMode: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, fakeConversation.GenerateCalls);
        Assert.Equal(GenerationPolicyMode.Grounded, fakeConversation.LastPolicyMode);
    }

    private sealed class FakeConversationService : IConversationService
    {
        public int GenerateCalls { get; private set; }

        public GenerationPolicyMode? LastPolicyMode { get; private set; }

        public Task<Result<ConversationReply>> GenerateReplyAsync(IRequestContext requestContext, ConversationGenerateRequest request, CancellationToken cancellationToken = default)
        {
            GenerateCalls++;
            LastPolicyMode = request.PolicyMode;

            var snapshot = new ConversationThreadSnapshot(
                request.ThreadId,
                [],
                new ConversationThreadState(request.ThreadId, null, null, null, DateTimeOffset.UtcNow, 0));

            return Task.FromResult(
                Result<ConversationReply>.Success(
                    new ConversationReply(
                        ThreadId: request.ThreadId,
                        AssistantMessage: "ok",
                        Snapshot: snapshot)));
        }

        public Task<Result<ConversationThreadSnapshot>> AddTurnAsync(IRequestContext requestContext, ConversationTurn turn, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<ConversationThreadSnapshot>> GetThreadAsync(IRequestContext requestContext, string threadId, int take = 50, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<ConversationThreadSnapshot>> RefreshThreadStateAsync(IRequestContext requestContext, string threadId, int recentWindow = 12, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
