using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Retrieval;

namespace TrueRag.UnitTests.Retrieval;

public sealed class RetrievalModuleRegistrationTests
{
    [Fact]
    public void AddTrueRagRetrieval_RegistersRetrievalService()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddTrueRagRetrieval();
        services.AddScoped<IRetrievalRepository, FakeRetrievalRepository>();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IRetrievalService>();

        Assert.NotNull(service);
    }

    private sealed class FakeRetrievalRepository : IRetrievalRepository
    {
        public Task<TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>> QueryVectorAsync(
            TrueRag.Core.Context.IRequestContext requestContext,
            TrueRag.Core.Models.RetrievalQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>.Success(new TrueRag.Core.Models.RetrievalResponse([])));

        public Task<TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>> QueryTextAsync(
            TrueRag.Core.Context.IRequestContext requestContext,
            TrueRag.Core.Models.RetrievalQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>.Success(new TrueRag.Core.Models.RetrievalResponse([])));

        public Task<TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>> QueryHybridAsync(
            TrueRag.Core.Context.IRequestContext requestContext,
            TrueRag.Core.Models.RetrievalQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>.Success(new TrueRag.Core.Models.RetrievalResponse([])));

        public Task<TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>> ExpandByLogicalSectionAsync(
            TrueRag.Core.Context.IRequestContext requestContext,
            IReadOnlyCollection<TrueRag.Core.Models.StructuralExpansionSeed> seeds,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>.Success(new TrueRag.Core.Models.RetrievalResponse([])));

        public Task<TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>> ExpandAdjacentChunksAsync(
            TrueRag.Core.Context.IRequestContext requestContext,
            IReadOnlyCollection<TrueRag.Core.Models.AdjacentExpansionSeed> seeds,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TrueRag.Core.Primitives.Result<TrueRag.Core.Models.RetrievalResponse>.Success(new TrueRag.Core.Models.RetrievalResponse([])));
    }
}
