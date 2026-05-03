using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Retrieval;

namespace TrueRag.UnitTests.Retrieval;

public sealed class RetrievalServiceTests
{
    [Fact]
    public async Task SearchVectorAsync_ValidQuery_CallsVectorRepository()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository);

        var result = await service.SearchVectorAsync(CreateContext(), new RetrievalQuery("any", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.VectorCalls);
        Assert.NotNull(result.Value?.RetrievalConfidence);
    }

    [Fact]
    public async Task SearchHybridAsync_MissingVector_ReturnsValidationError()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository);

        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("hello", null, 5));

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval.query_vector_required", result.Error?.Code);
        Assert.Equal(0, repository.HybridCalls);
    }

    [Fact]
    public async Task SearchHybridAsync_HighFidelityHit_TriggersStructuralExpansion()
    {
        var repository = new FakeRetrievalRepository
        {
            HybridResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc-1", "paragraph", "text", 0.9, "high", 1, null, "Document/Section3/Paragraph1")
            ])
        };

        var service = BuildService(repository);
        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("hello", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.StructuralExpansionCalls);
        Assert.Equal(0, repository.AdjacentExpansionCalls);
    }

    [Fact]
    public async Task SearchTextAsync_StandardFidelityHit_TriggersAdjacentExpansion_WhenFallbackEnabled()
    {
        var repository = new FakeRetrievalRepository
        {
            TextResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc-1", "paragraph", "text", 0.9, "standard", 1, null, null)
            ])
        };

        var service = BuildService(repository, fallbackToStandardRag: true);
        var result = await service.SearchTextAsync(CreateContext(), new RetrievalQuery("hello", null, 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.AdjacentExpansionCalls);
    }

    [Fact]
    public async Task SearchTextAsync_StandardFidelityHit_DoesNotExpand_WhenFallbackDisabled()
    {
        var repository = new FakeRetrievalRepository
        {
            TextResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc-1", "paragraph", "text", 0.9, "standard", 1, null, null)
            ])
        };

        var service = BuildService(repository, fallbackToStandardRag: false);
        var result = await service.SearchTextAsync(CreateContext(), new RetrievalQuery("hello", null, 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repository.AdjacentExpansionCalls);
    }

    [Fact]
    public async Task SearchVectorAsync_RequireHighFidelity_InjectsFidelityFilter()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository, requireHighFidelity: true);

        var result = await service.SearchVectorAsync(CreateContext(), new RetrievalQuery("any", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Equal("high", repository.LastQueryFilters?["fidelity_level"]);
    }

    [Fact]
    public async Task SearchHybridAsync_WithReferencedIds_ExpandsMultiHopNodes()
    {
        var repository = new FakeRetrievalRepository
        {
            HybridResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc-1", "paragraph", "text", 0.9, "high", 1, null, "Document/Section3/Paragraph1", ReferencedNodeIds: ["n2"])
            ]),
            ReferencedNodes = [
                new RetrievedNode("n2", "doc-1", "table", "table", 0.7, "high", 2, null, "Document/Section4/Table1")
            ]
        };

        var service = BuildService(repository, enableMultiHop: true);
        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("hello", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.GetNodesByIdsCalls);
        Assert.Contains(result.Value!.Nodes, static n => n.NodeId == "n2");
    }

    [Fact]
    public async Task SearchTextAsync_WithDiffFilters_AttachesDiffs()
    {
        var repository = new FakeRetrievalRepository
        {
            TextResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc-1", "paragraph", "text", 0.9, "standard", 1, null, "Document/Section3/Paragraph1")
            ]),
            Diffs = [
                new StructuralDiffResult("contract", "v1", "v2", "Section/4.1", "left", "right", "--- left\n+++ right\n-left\n+right")
            ]
        };

        var service = BuildService(repository, enableDiffing: true);
        var query = new RetrievalQuery("hello", null, 5, new Dictionary<string, string>
        {
            ["document_group_id"] = "contract",
            ["left_version"] = "v1",
            ["right_version"] = "v2",
            ["logical_path"] = "Section/4.1"
        });

        var result = await service.SearchTextAsync(CreateContext(), query);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.GetStructuralDiffsCalls);
        Assert.NotNull(result.Value!.Diffs);
        Assert.Single(result.Value.Diffs!);
    }

    private static IRetrievalService BuildService(
        FakeRetrievalRepository repository,
        bool requireHighFidelity = false,
        bool fallbackToStandardRag = true,
        bool enableMultiHop = true,
        bool enableDiffing = true)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RetrievalEngine:RequireHighFidelity"] = requireHighFidelity.ToString(),
                ["RetrievalEngine:FallbackToStandardRag"] = fallbackToStandardRag.ToString(),
                ["RetrievalEngine:EnableMultiHopLinking"] = enableMultiHop.ToString(),
                ["RetrievalEngine:EnableStructuralDiffing"] = enableDiffing.ToString(),
                ["RetrievalEngine:EnableSemanticCache"] = "false",
                ["RetrievalEngine:EnableDistributedRateLimit"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddDistributedMemoryCache();
        services.AddTrueRagRetrieval();
        services.AddScoped<IRetrievalRepository>(_ => repository);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IRetrievalService>();
    }

    private static IRequestContext CreateContext()
        => new RequestContext("tenant-1", "app-1", "user-1", ["reader"], ["group-1"]);

    private sealed class FakeRetrievalRepository : IRetrievalRepository
    {
        public int VectorCalls { get; private set; }

        public int TextCalls { get; private set; }

        public int HybridCalls { get; private set; }

        public int StructuralExpansionCalls { get; private set; }

        public int AdjacentExpansionCalls { get; private set; }

        public IReadOnlyDictionary<string, string>? LastQueryFilters { get; private set; }

        public RetrievalResponse VectorResult { get; set; } = new([]);

        public RetrievalResponse TextResult { get; set; } = new([]);

        public RetrievalResponse HybridResult { get; set; } = new([]);

        public RetrievalResponse ExpansionResult { get; set; } = new([]);

        public IReadOnlyCollection<RetrievedNode> ReferencedNodes { get; set; } = [];

        public IReadOnlyCollection<StructuralDiffResult> Diffs { get; set; } = [];

        public int GetNodesByIdsCalls { get; private set; }

        public int GetStructuralDiffsCalls { get; private set; }

        public Task<Result<RetrievalResponse>> QueryVectorAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            VectorCalls++;
            LastQueryFilters = query.Filters;
            return Task.FromResult(Result<RetrievalResponse>.Success(VectorResult));
        }

        public Task<Result<RetrievalResponse>> QueryTextAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            TextCalls++;
            LastQueryFilters = query.Filters;
            return Task.FromResult(Result<RetrievalResponse>.Success(TextResult));
        }

        public Task<Result<RetrievalResponse>> QueryHybridAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            HybridCalls++;
            LastQueryFilters = query.Filters;
            return Task.FromResult(Result<RetrievalResponse>.Success(HybridResult));
        }

        public Task<Result<RetrievalResponse>> ExpandByLogicalSectionAsync(IRequestContext requestContext, IReadOnlyCollection<StructuralExpansionSeed> seeds, int limit, CancellationToken cancellationToken = default)
        {
            StructuralExpansionCalls++;
            return Task.FromResult(Result<RetrievalResponse>.Success(ExpansionResult));
        }

        public Task<Result<RetrievalResponse>> ExpandAdjacentChunksAsync(IRequestContext requestContext, IReadOnlyCollection<AdjacentExpansionSeed> seeds, int limit, CancellationToken cancellationToken = default)
        {
            AdjacentExpansionCalls++;
            return Task.FromResult(Result<RetrievalResponse>.Success(ExpansionResult));
        }

        public Task<Result<IReadOnlyCollection<RetrievedNode>>> GetNodesByIdsAsync(IRequestContext requestContext, IReadOnlyCollection<string> nodeIds, CancellationToken cancellationToken = default)
        {
            GetNodesByIdsCalls++;
            return Task.FromResult(Result<IReadOnlyCollection<RetrievedNode>>.Success(ReferencedNodes));
        }

        public Task<Result<IReadOnlyCollection<StructuralDiffResult>>> GetStructuralDiffsAsync(IRequestContext requestContext, IReadOnlyCollection<StructuralDiffRequest> requests, CancellationToken cancellationToken = default)
        {
            GetStructuralDiffsCalls++;
            return Task.FromResult(Result<IReadOnlyCollection<StructuralDiffResult>>.Success(Diffs));
        }
    }
}
