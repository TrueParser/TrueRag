using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Conversations;
using TrueRag.Core.Abstractions;

namespace TrueRag.UnitTests.Conversations;

public sealed class ConversationModuleRegistrationTests
{
    [Fact]
    public void AddTrueRagConversations_RegistersConversationService()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddTrueRagConversations();
        services.AddScoped<IConversationRepository, NoopConversationRepository>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IConversationService>();

        Assert.NotNull(service);
    }

    private sealed class NoopConversationRepository : IConversationRepository
    {
        public Task<TrueRag.Core.Primitives.Result> AppendMessageAsync(TrueRag.Core.Context.IRequestContext requestContext, TrueRag.Core.Models.ConversationMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result.Success());

        public Task<TrueRag.Core.Primitives.Result<IReadOnlyCollection<TrueRag.Core.Models.ConversationMessage>>> GetThreadAsync(TrueRag.Core.Context.IRequestContext requestContext, string threadId, int take, CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result<IReadOnlyCollection<TrueRag.Core.Models.ConversationMessage>>.Success([]));

        public Task<TrueRag.Core.Primitives.Result<TrueRag.Core.Models.ConversationThreadState?>> GetThreadStateAsync(TrueRag.Core.Context.IRequestContext requestContext, string threadId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result<TrueRag.Core.Models.ConversationThreadState?>.Success(null));

        public Task<TrueRag.Core.Primitives.Result> UpsertThreadStateAsync(TrueRag.Core.Context.IRequestContext requestContext, TrueRag.Core.Models.ConversationThreadState state, CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result.Success());
    }
}
