using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Retrieval;

namespace TrueRag.IntegrationTests.Retrieval;

public sealed class FidelityBranchingIntegrationTests
{
    [Fact]
    public async Task SearchHybridAsync_HighFidelityResult_UsesStructuralExpansion()
    {
        var repository = new CaptureRepository
        {
            HybridResponse = new RetrievalResponse([
                new RetrievedNode("n1", "doc-1", "paragraph", "text", 0.9, "high", 1, null, "Document/Section1/Paragraph1")
            ])
        };

        using var provider = BuildProvider(repository, requireHighFidelity: false, fallbackToStandard: true);
        var service = provider.GetRequiredService<IRetrievalService>();

        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("risk", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.StructuralExpansionCalls);
        Assert.Equal(0, repository.AdjacentExpansionCalls);
    }

    private static ServiceProvider BuildProvider(CaptureRepository repository, bool requireHighFidelity, bool fallbackToStandard)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RetrievalEngine:RequireHighFidelity"] = requireHighFidelity.ToString(),
                ["RetrievalEngine:FallbackToStandardRag"] = fallbackToStandard.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddTrueRagRetrieval();
        services.AddScoped<IRetrievalRepository>(_ => repository);
        return services.BuildServiceProvider();
    }

    private static IRequestContext CreateContext() =>
        new RequestContext("tenant-1", "app-1", "user-1", ["reader"], ["group-1"]);

    private sealed class CaptureRepository : IRetrievalRepository
    {
        public RetrievalResponse HybridResponse { get; set; } = new([]);

        public int StructuralExpansionCalls { get; private set; }

        public int AdjacentExpansionCalls { get; private set; }

        public Task<Result<RetrievalResponse>> QueryVectorAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<RetrievalResponse>.Success(new RetrievalResponse([])));

        public Task<Result<RetrievalResponse>> QueryTextAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<RetrievalResponse>.Success(new RetrievalResponse([])));

        public Task<Result<RetrievalResponse>> QueryHybridAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<RetrievalResponse>.Success(HybridResponse));

        public Task<Result<RetrievalResponse>> ExpandByLogicalSectionAsync(IRequestContext requestContext, IReadOnlyCollection<StructuralExpansionSeed> seeds, int limit, CancellationToken cancellationToken = default)
        {
            StructuralExpansionCalls++;
            return Task.FromResult(Result<RetrievalResponse>.Success(new RetrievalResponse([])));
        }

        public Task<Result<RetrievalResponse>> ExpandAdjacentChunksAsync(IRequestContext requestContext, IReadOnlyCollection<AdjacentExpansionSeed> seeds, int limit, CancellationToken cancellationToken = default)
        {
            AdjacentExpansionCalls++;
            return Task.FromResult(Result<RetrievalResponse>.Success(new RetrievalResponse([])));
        }
    }
}
