using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Retrieval;

namespace TrueRag.IntegrationTests.Retrieval;

public sealed class HybridFusionModeIntegrationTests
{
    [Fact]
    public async Task SearchHybridAsync_SqlMode_UsesRepositoryHybridPath()
    {
        var repository = new CaptureHybridRepository();
        using var provider = BuildProvider(repository, "Sql");
        var service = provider.GetRequiredService<IRetrievalService>();

        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("budget", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.HybridCalls);
        Assert.Equal(0, repository.VectorCalls);
        Assert.Equal(0, repository.TextCalls);
    }

    [Fact]
    public async Task SearchHybridAsync_SplitRrfMode_UsesVectorAndTextLanes()
    {
        var repository = new CaptureHybridRepository
        {
            VectorResponse = new RetrievalResponse([
                new RetrievedNode("n1", "doc-1", "paragraph", "v", 0.9d, "standard", null, null, null)
            ]),
            TextResponse = new RetrievalResponse([
                new RetrievedNode("n2", "doc-2", "paragraph", "t", 0.9d, "standard", null, null, null)
            ])
        };

        using var provider = BuildProvider(repository, "SplitRrf");
        var service = provider.GetRequiredService<IRetrievalService>();

        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("budget", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repository.HybridCalls);
        Assert.Equal(1, repository.VectorCalls);
        Assert.Equal(1, repository.TextCalls);
    }

    private static ServiceProvider BuildProvider(CaptureHybridRepository repository, string hybridMode)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RetrievalEngine:RequireHighFidelity"] = "false",
                ["RetrievalEngine:FallbackToStandardRag"] = "true",
                ["RetrievalEngine:EnableSemanticCache"] = "false",
                ["RetrievalEngine:EnableDistributedRateLimit"] = "false",
                ["RetrievalEngine:EnableMultiHopLinking"] = "false",
                ["RetrievalEngine:EnableStructuralDiffing"] = "false",
                ["RetrievalEngine:HybridFusionMode"] = hybridMode
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDistributedMemoryCache();
        services.AddLogging();
        services.AddTrueRagRetrieval();
        services.AddScoped<IRetrievalRepository>(_ => repository);
        return services.BuildServiceProvider();
    }

    private static IRequestContext CreateContext()
        => new RequestContext("tenant-a", "app-a", "user-1", ["reader"], ["group-1"], "collection-main");

    private sealed class CaptureHybridRepository : IRetrievalRepository
    {
        public int VectorCalls { get; private set; }

        public int TextCalls { get; private set; }

        public int HybridCalls { get; private set; }

        public RetrievalResponse VectorResponse { get; set; } = new([]);

        public RetrievalResponse TextResponse { get; set; } = new([]);

        public RetrievalResponse HybridResponse { get; set; } = new([]);

        public Task<Result<RetrievalResponse>> QueryVectorAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            VectorCalls++;
            return Task.FromResult(Result<RetrievalResponse>.Success(VectorResponse));
        }

        public Task<Result<RetrievalResponse>> QueryTextAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            TextCalls++;
            return Task.FromResult(Result<RetrievalResponse>.Success(TextResponse));
        }

        public Task<Result<RetrievalResponse>> QueryHybridAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            HybridCalls++;
            return Task.FromResult(Result<RetrievalResponse>.Success(HybridResponse));
        }

        public Task<Result<RetrievalResponse>> ExpandByLogicalSectionAsync(IRequestContext requestContext, IReadOnlyCollection<StructuralExpansionSeed> seeds, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<RetrievalResponse>.Success(new RetrievalResponse([])));

        public Task<Result<RetrievalResponse>> ExpandAdjacentChunksAsync(IRequestContext requestContext, IReadOnlyCollection<AdjacentExpansionSeed> seeds, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<RetrievalResponse>.Success(new RetrievalResponse([])));

        public Task<Result<IReadOnlyCollection<RetrievedNode>>> GetNodesByIdsAsync(IRequestContext requestContext, IReadOnlyCollection<string> nodeIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyCollection<RetrievedNode>>.Success([]));

        public Task<Result<IReadOnlyCollection<StructuralDiffResult>>> GetStructuralDiffsAsync(IRequestContext requestContext, IReadOnlyCollection<StructuralDiffRequest> requests, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyCollection<StructuralDiffResult>>.Success([]));
    }
}
