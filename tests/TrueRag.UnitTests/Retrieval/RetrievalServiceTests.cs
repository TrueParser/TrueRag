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
    public async Task SearchVectorAsync_ExternalMode_MissingText_ReturnsValidationError()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository);

        var result = await service.SearchVectorAsync(CreateContext(), new RetrievalQuery(string.Empty, [0.1f], 5));

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval.query_text_required", result.Error?.Code);
        Assert.Equal(0, repository.VectorCalls);
    }

    [Fact]
    public async Task SearchVectorAsync_InternalMode_TextOnly_GeneratesVectorAndQueries()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(
            repository,
            embeddingModeResolver: new TestEmbeddingModeResolver(CollectionEmbeddingMode.InternalEmbedding),
            queryEmbeddingGenerator: new TestQueryEmbeddingGenerator([0.7f, 0.8f]),
            embeddingProfileResolver: new TestEmbeddingProfileResolver(2));

        var result = await service.SearchVectorAsync(CreateContext(), new RetrievalQuery("hello", null, 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.VectorCalls);
    }

    [Fact]
    public async Task SearchVectorAsync_DescriptorMismatch_ReturnsValidationError()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(
            repository,
            embeddingModeResolver: new TestEmbeddingModeResolver(CollectionEmbeddingMode.ExternalEmbedding),
            embeddingProfileResolver: new TestEmbeddingProfileResolver(4));

        var result = await service.SearchVectorAsync(CreateContext(), new RetrievalQuery("hello", [0.1f, 0.2f], 5));

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval.embedding_space_mismatch", result.Error?.Code);
        Assert.Equal(0, repository.VectorCalls);
    }

    [Fact]
    public async Task SearchVectorAsync_QueryProviderMismatch_ReturnsValidationError()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(
            repository,
            embeddingModeResolver: new TestEmbeddingModeResolver(CollectionEmbeddingMode.ExternalEmbedding),
            embeddingProfileResolver: new TestEmbeddingProfileResolver(2, provider: "onnx", model: "test-model"));

        var result = await service.SearchVectorAsync(CreateContext(), new RetrievalQuery("hello", [0.1f, 0.2f], 5, QueryVectorProvider: "openai"));

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval.embedding_space_mismatch", result.Error?.Code);
        Assert.Equal(0, repository.VectorCalls);
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
    public async Task SearchHybridAsync_OmittedHybridWeights_AppliesDefaults()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository);

        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("hello", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.LastHybridQuery);
        Assert.Equal(1d, repository.LastHybridQuery!.VectorWeight);
        Assert.Equal(1d, repository.LastHybridQuery.TextWeight);
        Assert.Equal(60, repository.LastHybridQuery.RrfK);
    }

    [Fact]
    public async Task SearchHybridAsync_InvalidVectorWeight_ReturnsValidationError()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository);
        var query = new RetrievalQuery("hello", [0.1f], 5, VectorWeight: -0.1d);

        var result = await service.SearchHybridAsync(CreateContext(), query);

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval.hybrid_vector_weight_invalid", result.Error?.Code);
        Assert.Equal(0, repository.HybridCalls);
    }

    [Fact]
    public async Task SearchHybridAsync_InvalidRrfK_ReturnsValidationError()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository);
        var query = new RetrievalQuery("hello", [0.1f], 5, RrfK: 0);

        var result = await service.SearchHybridAsync(CreateContext(), query);

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval.hybrid_rrfk_invalid", result.Error?.Code);
        Assert.Equal(0, repository.HybridCalls);
    }

    [Fact]
    public async Task SearchHybridAsync_BothWeightsZero_ReturnsValidationError()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository);
        var query = new RetrievalQuery("hello", [0.1f], 5, VectorWeight: 0d, TextWeight: 0d);

        var result = await service.SearchHybridAsync(CreateContext(), query);

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval.hybrid_weight_sum_invalid", result.Error?.Code);
        Assert.Equal(0, repository.HybridCalls);
    }

    [Fact]
    public async Task SearchHybridAsync_SplitRrfMode_FusesVectorAndTextLanes()
    {
        var repository = new FakeRetrievalRepository
        {
            VectorResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc", "paragraph", "v1", 0.9, "standard", null, null, null),
                new RetrievedNode("n2", "doc", "paragraph", "v2", 0.8, "standard", null, null, null)
            ]),
            TextResult = new RetrievalResponse([
                new RetrievedNode("n2", "doc", "paragraph", "t2", 0.7, "standard", null, null, null),
                new RetrievedNode("n1", "doc", "paragraph", "t1", 0.6, "standard", null, null, null)
            ])
        };

        var service = BuildService(repository, hybridFusionMode: "SplitRrf");
        var query = new RetrievalQuery("hello", [0.1f], 5, VectorWeight: 1d, TextWeight: 3d, RrfK: 60);

        var result = await service.SearchHybridAsync(CreateContext(), query);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.VectorCalls);
        Assert.Equal(1, repository.TextCalls);
        Assert.Equal(0, repository.HybridCalls);
        Assert.Collection(
            result.Value!.Nodes,
            n => Assert.Equal("n2", n.NodeId),
            n => Assert.Equal("n1", n.NodeId));
    }

    [Fact]
    public async Task SearchHybridAsync_SplitRrfMode_UsesDeterministicTieBreakByNodeId()
    {
        var repository = new FakeRetrievalRepository
        {
            VectorResult = new RetrievalResponse([
                new RetrievedNode("b-node", "doc", "paragraph", "v", 0.9, "standard", null, null, null)
            ]),
            TextResult = new RetrievalResponse([
                new RetrievedNode("a-node", "doc", "paragraph", "t", 0.9, "standard", null, null, null)
            ])
        };

        var service = BuildService(repository, hybridFusionMode: "SplitRrf");
        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("hello", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Value!.Nodes,
            n => Assert.Equal("a-node", n.NodeId),
            n => Assert.Equal("b-node", n.NodeId));
    }

    [Fact]
    public async Task SearchHybridAsync_SplitRrfMode_ZeroTextLane_FallsBackToVectorLane()
    {
        var repository = new FakeRetrievalRepository
        {
            VectorResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc", "paragraph", "v1", 0.9, "standard", null, null, null)
            ]),
            TextResult = new RetrievalResponse([])
        };

        var service = BuildService(repository, hybridFusionMode: "SplitRrf");
        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("hello", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Nodes);
        Assert.Equal("n1", result.Value.Nodes.First().NodeId);
    }

    [Fact]
    public async Task SearchHybridAsync_ClampGuardrailMode_ClampsExtremeInputs()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository, hybridGuardrailMode: "Clamp");
        var query = new RetrievalQuery("hello", [0.1f], 5, VectorWeight: -4d, TextWeight: 999d, RrfK: 10000);

        var result = await service.SearchHybridAsync(CreateContext(), query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.LastHybridQuery);
        Assert.Equal(0d, repository.LastHybridQuery!.VectorWeight);
        Assert.Equal(10d, repository.LastHybridQuery.TextWeight);
        Assert.Equal(500, repository.LastHybridQuery.RrfK);
    }

    [Fact]
    public async Task SearchHybridAsync_SplitRrfMode_UsesCandidateLimitForLaneQueries()
    {
        var repository = new FakeRetrievalRepository
        {
            VectorResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc", "paragraph", "v1", 0.9, "standard", null, null, null)
            ]),
            TextResult = new RetrievalResponse([
                new RetrievedNode("n1", "doc", "paragraph", "t1", 0.9, "standard", null, null, null)
            ])
        };

        var service = BuildService(repository, hybridFusionMode: "SplitRrf", hybridCandidateLimit: 100);
        var result = await service.SearchHybridAsync(CreateContext(), new RetrievalQuery("hello", [0.1f], 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(100, repository.LastVectorQuery?.TopK);
        Assert.Equal(100, repository.LastTextQuery?.TopK);
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

    [Fact]
    public async Task SearchTextAsync_RejectsWhenQueryCollectionMismatchesRequestScope()
    {
        var repository = new FakeRetrievalRepository();
        var service = BuildService(repository);
        var query = new RetrievalQuery("hello", null, 5, CollectionId: "other-collection");

        var result = await service.SearchTextAsync(CreateContext(), query);

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval.collection_scope_mismatch", result.Error?.Code);
        Assert.Equal(0, repository.TextCalls);
    }

    private static IRetrievalService BuildService(
        FakeRetrievalRepository repository,
        bool requireHighFidelity = false,
        bool fallbackToStandardRag = true,
        bool enableMultiHop = true,
        bool enableDiffing = true,
        string hybridFusionMode = "Sql",
        int hybridCandidateLimit = 100,
        string hybridGuardrailMode = "Reject",
        ICollectionEmbeddingModeResolver? embeddingModeResolver = null,
        IQueryEmbeddingGenerator? queryEmbeddingGenerator = null,
        IEmbeddingProfileResolver? embeddingProfileResolver = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RetrievalEngine:RequireHighFidelity"] = requireHighFidelity.ToString(),
                ["RetrievalEngine:FallbackToStandardRag"] = fallbackToStandardRag.ToString(),
                ["RetrievalEngine:EnableMultiHopLinking"] = enableMultiHop.ToString(),
                ["RetrievalEngine:EnableStructuralDiffing"] = enableDiffing.ToString(),
                ["RetrievalEngine:EnableSemanticCache"] = "false",
                ["RetrievalEngine:EnableDistributedRateLimit"] = "false",
                ["RetrievalEngine:HybridFusionMode"] = hybridFusionMode,
                ["RetrievalEngine:HybridCandidateLimit"] = hybridCandidateLimit.ToString(),
                ["RetrievalEngine:HybridDefaultVectorWeight"] = "1.0",
                ["RetrievalEngine:HybridDefaultTextWeight"] = "1.0",
                ["RetrievalEngine:HybridDefaultRrfK"] = "60",
                ["RetrievalEngine:HybridMinWeight"] = "0.0",
                ["RetrievalEngine:HybridMaxWeight"] = "10.0",
                ["RetrievalEngine:HybridMinRrfK"] = "1",
                ["RetrievalEngine:HybridMaxRrfK"] = "500",
                ["RetrievalEngine:HybridGuardrailMode"] = hybridGuardrailMode
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddTrueRagRetrieval();
        services.AddScoped<IRetrievalRepository>(_ => repository);
        if (embeddingModeResolver is not null)
        {
            services.AddSingleton(embeddingModeResolver);
        }

        if (queryEmbeddingGenerator is not null)
        {
            services.AddSingleton(queryEmbeddingGenerator);
        }

        if (embeddingProfileResolver is not null)
        {
            services.AddSingleton(embeddingProfileResolver);
        }

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IRetrievalService>();
    }

    private static IRequestContext CreateContext()
        => new RequestContext("tenant-1", "app-1", "user-1", ["reader"], ["group-1"], "collection-1");

    private sealed class FakeRetrievalRepository : IRetrievalRepository
    {
        public int VectorCalls { get; private set; }

        public int TextCalls { get; private set; }

        public int HybridCalls { get; private set; }

        public RetrievalQuery? LastHybridQuery { get; private set; }

        public RetrievalQuery? LastVectorQuery { get; private set; }

        public RetrievalQuery? LastTextQuery { get; private set; }

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
            LastVectorQuery = query;
            LastQueryFilters = query.Filters;
            return Task.FromResult(Result<RetrievalResponse>.Success(VectorResult));
        }

        public Task<Result<RetrievalResponse>> QueryTextAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            TextCalls++;
            LastTextQuery = query;
            LastQueryFilters = query.Filters;
            return Task.FromResult(Result<RetrievalResponse>.Success(TextResult));
        }

        public Task<Result<RetrievalResponse>> QueryHybridAsync(IRequestContext requestContext, RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            HybridCalls++;
            LastHybridQuery = query;
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

    private sealed class TestEmbeddingModeResolver(CollectionEmbeddingMode mode) : ICollectionEmbeddingModeResolver
    {
        public Task<CollectionEmbeddingMode> ResolveModeAsync(IRequestContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(mode);
    }

    private sealed class TestQueryEmbeddingGenerator(float[] vector) : IQueryEmbeddingGenerator
    {
        public Task<Result<float[]>> GenerateQueryVectorAsync(IRequestContext context, string queryText, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<float[]>.Success(vector));
    }

    private sealed class TestEmbeddingProfileResolver(int dimensions, string provider = "onnx", string model = "test-model") : IEmbeddingProfileResolver
    {
        public Task<EmbeddingModelDescriptor> ResolveActiveDescriptorAsync(string tenantId, string appId, string collectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new EmbeddingModelDescriptor(provider, model, dimensions, 512, EmbeddingDistanceMetric.Cosine));
    }
}
