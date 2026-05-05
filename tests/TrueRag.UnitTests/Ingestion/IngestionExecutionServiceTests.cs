using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion;
using TrueRag.Ingestion.Admission;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Execution;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;

namespace TrueRag.UnitTests.Ingestion;

public sealed class IngestionExecutionServiceTests
{
    [Fact]
    public async Task IngestAsyncBuffered_AppendsWalAndPublishesNodeScopedMessage()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var request = CreateRequest();
        var context = CreateContext();

        var result = await service.IngestAsyncBuffered(context, request);

        Assert.True(result.IsSuccess);

        var queue = scope.ServiceProvider.GetRequiredService<TestQueuePublisher>();
        Assert.Single(queue.Published);
        Assert.Equal("TrueRAG.Job.Ingest.node-test", queue.Published[0].Topic);
        Assert.True(result.Value!.RequiresInternalEmbeddingGeneration);
        Assert.False(result.Value!.UsesPrecomputedVectors);

        var wal = scope.ServiceProvider.GetRequiredService<TestAcceptanceLog>();
        Assert.Equal(1, wal.AppendCalls);
        Assert.Equal("standard", wal.LastSerializedPayload?.Fidelity);
        Assert.Equal("external_embedding", wal.LastSerializedPayload?.EmbeddingModeTag);
        Assert.NotNull(wal.LastMetadata);
        Assert.True(wal.LastMetadata!.RequiresInternalEmbeddingGeneration);
        Assert.False(wal.LastMetadata.UsesPrecomputedVectors);
    }

    [Fact]
    public async Task IngestAsyncBuffered_RejectsWhenChunkVectorProvided()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var result = await service.IngestAsyncBuffered(CreateContext(), CreateRequestWithVector(documentId: "doc-v"));

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.async_precomputed_vectors_not_allowed", result.Error?.Code);
    }

    [Fact]
    public async Task IngestSyncAsync_PersistsDirectlyWithoutQueuePublish()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var request = CreateRequestWithVector();
        var context = CreateContext();

        var result = await service.IngestSyncAsync(context, request);

        Assert.True(result.IsSuccess);

        var repository = scope.ServiceProvider.GetRequiredService<TestIngestionRepository>();
        Assert.Equal(1, repository.UpsertCalls);
        Assert.Equal("standard", repository.LastDocument?.Fidelity);
        Assert.Equal("external_embedding", repository.LastDocument?.EmbeddingModeTag);

        var queue = scope.ServiceProvider.GetRequiredService<TestQueuePublisher>();
        Assert.Empty(queue.Published);
    }

    [Fact]
    public async Task IngestSyncAsync_RejectsWhenChunkVectorMissing()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var request = CreateRequestWithEmptyVector();
        var context = CreateContext();

        var result = await service.IngestSyncAsync(context, request);

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.sync_precomputed_vectors_required", result.Error?.Code);

        var repository = scope.ServiceProvider.GetRequiredService<TestIngestionRepository>();
        Assert.Equal(0, repository.UpsertCalls);
    }

    [Fact]
    public async Task IngestSyncAsync_InternalMode_RejectsSyncPath()
    {
        var services = CreateServices(
            modeResolver: new TestCollectionEmbeddingModeResolver(CollectionEmbeddingMode.InternalEmbedding));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var result = await service.IngestSyncAsync(CreateContext(), CreateRequestWithVector());

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.sync_disabled_for_internal_embedding_mode", result.Error?.Code);
    }

    [Fact]
    public async Task IngestSyncAsync_DescriptorMismatch_RejectsWithEmbeddingSpaceError()
    {
        var services = CreateServices(
            profileResolver: new TestEmbeddingProfileResolver(4));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var result = await service.IngestSyncAsync(CreateContext(), CreateRequestWithVector());

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.embedding_space_mismatch", result.Error?.Code);
    }

    [Fact]
    public async Task IngestSyncAsync_PrecomputedProviderMismatch_RejectsWithEmbeddingSpaceError()
    {
        var services = CreateServices(profileResolver: new TestEmbeddingProfileResolver(1, provider: "onnx", model: "test-model"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var request = CreateRequestWithVector(precomputedProvider: "openai");
        var result = await service.IngestSyncAsync(CreateContext(), request);

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.embedding_space_mismatch", result.Error?.Code);
    }

    [Fact]
    public async Task IngestAsyncBuffered_RejectsWhenFamilyQueueDepthExhausted()
    {
        var services = CreateServices(
            backpressure: new Dictionary<string, string?>
            {
                ["IngestionBackpressure:MaxFamilyQueueDepth"] = "1",
                ["IngestionBackpressure:MinDepthBeforeDrainRatioReject"] = "1000"
            });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var context = CreateContext();

        var first = await service.IngestAsyncBuffered(context, CreateRequest(documentId: "doc-1"));
        var second = await service.IngestAsyncBuffered(context, CreateRequest(documentId: "doc-2"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("queue_depth_exhausted", second.Error?.Code);
    }

    [Fact]
    public async Task IngestAsyncBuffered_RejectsWhenDrainRatioIndicatesBackpressure()
    {
        var services = CreateServices(
            backpressure: new Dictionary<string, string?>
            {
                ["IngestionBackpressure:MaxFamilyQueueDepth"] = "100",
                ["IngestionBackpressure:MinDepthBeforeDrainRatioReject"] = "1",
                ["IngestionBackpressure:DrainCapacityRatioRejectThreshold"] = "1.0"
            });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IIngestionPressureTracker>();
        tracker.RecordAccepted();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var result = await service.IngestAsyncBuffered(CreateContext(), CreateRequest(documentId: "doc-1"));

        Assert.True(result.IsFailure);
        Assert.Equal("wal_backpressure_high", result.Error?.Code);
    }

    [Fact]
    public async Task IngestAsyncBuffered_RejectsWhenPayloadCollectionMismatchesRequestScope()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var context = CreateContext();
        var request = CreateRequest(documentId: "doc-x", collectionId: "other-collection");

        var result = await service.IngestAsyncBuffered(context, request);

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.collection_scope_mismatch", result.Error?.Code);
    }

    private static ServiceCollection CreateServices(
        Dictionary<string, string?>? backpressure = null,
        ICollectionEmbeddingModeResolver? modeResolver = null,
        IEmbeddingProfileResolver? profileResolver = null)
    {
        var configValues = new Dictionary<string, string?>();
        if (backpressure is not null)
        {
            foreach (var pair in backpressure)
            {
                configValues[pair.Key] = pair.Value;
            }
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddOptions();
        services.Configure<IngestionRuntimeOptions>(options =>
        {
            options.NodeId = "node-test";
            options.WalRootPath = Path.Combine(Path.GetTempPath(), "truerag-tests");
            options.SyncMaxConcurrency = 2;
        });

        services.AddTrueRagIngestion();

        services.AddSingleton<TestAcceptanceLog>();
        services.AddSingleton<TestQueuePublisher>();
        services.AddSingleton<TestIngestionRepository>();

        services.AddSingleton<IIngestionAcceptanceLog>(sp => sp.GetRequiredService<TestAcceptanceLog>());
        services.AddSingleton<IQueuePublisher>(sp => sp.GetRequiredService<TestQueuePublisher>());
        services.AddScoped<IIngestionRepository>(sp => sp.GetRequiredService<TestIngestionRepository>());
        if (modeResolver is not null)
        {
            services.AddSingleton(modeResolver);
        }

        if (profileResolver is not null)
        {
            services.AddSingleton(profileResolver);
        }

        return services;
    }

    private static IngestionRequestDto CreateRequest(string documentId = "doc-1", string? collectionId = null, string? precomputedProvider = null, string? precomputedModel = null)
    {
        var chunk = new ChunkDto(
            "node-1",
            null,
            null,
            "paragraph",
            "content",
            null,
            null,
            []);

        return new IngestionRequestDto(
            documentId,
            "group-1",
            "1",
            ["group-1"],
            "auto",
            [chunk],
            collectionId,
            null,
            precomputedProvider,
            precomputedModel);
    }

    private static IngestionRequestDto CreateRequestWithEmptyVector(string documentId = "doc-1")
    {
        var chunk = new ChunkDto(
            "node-1",
            null,
            null,
            "paragraph",
            "content",
            null,
            null,
            []);

        return new IngestionRequestDto(
            documentId,
            "group-1",
            "1",
            ["group-1"],
            "auto",
            [chunk],
            "collection-1");
    }

    private static IngestionRequestDto CreateRequestWithVector(string documentId = "doc-1", string? collectionId = "collection-1", string? precomputedProvider = null, string? precomputedModel = null)
    {
        var chunk = new ChunkDto(
            "node-1",
            null,
            null,
            "paragraph",
            "content",
            null,
            null,
            [0.1f]);

        return new IngestionRequestDto(
            documentId,
            "group-1",
            "1",
            ["group-1"],
            "auto",
            [chunk],
            collectionId,
            null,
            precomputedProvider,
            precomputedModel);
    }

    private static RequestContext CreateContext()
        => new("tenant-1", "app-1", "user-1", ["reader"], ["group-1"], "collection-1");

    private sealed class TestAcceptanceLog : IIngestionAcceptanceLog
    {
        public int AppendCalls { get; private set; }

        public IngestionRequestDto? LastSerializedPayload { get; private set; }
        public IngestionWalRecordMetadata? LastMetadata { get; private set; }

        public Task<IngestionWalAppendResult> AppendAsync(
            IngestionWalRecordMetadata metadata,
            Stream payload,
            long payloadLength,
            CancellationToken cancellationToken = default)
        {
            AppendCalls++;
            LastMetadata = metadata;

            using var memory = new MemoryStream();
            payload.CopyTo(memory);
            LastSerializedPayload = JsonSerializer.Deserialize<IngestionRequestDto>(memory.ToArray());

            return Task.FromResult(new IngestionWalAppendResult(
                metadata.TenantId,
                metadata.AppId,
                metadata.CollectionId,
                "tenant-1:app-1:shard-1",
                "wal/segment.wal",
                "segment-1",
                16,
                128,
                1));
        }

        public IEnumerable<string> EnumerateLaneFiles()
            => [];
    }

    private sealed class TestQueuePublisher : IQueuePublisher
    {
        public List<(string Topic, object Message)> Published { get; } = [];

        public Task PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default)
        {
            Published.Add((topic, message!));
            return Task.CompletedTask;
        }
    }

    private sealed class TestIngestionRepository : IIngestionRepository
    {
        public int UpsertCalls { get; private set; }

        public IngestionRequestDto? LastDocument { get; private set; }

        public Task<Result> UpsertDocumentAsync(
            IRequestContext context,
            IngestionRequestDto document,
            CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            LastDocument = document;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TestCollectionEmbeddingModeResolver(CollectionEmbeddingMode mode) : ICollectionEmbeddingModeResolver
    {
        public Task<CollectionEmbeddingMode> ResolveModeAsync(IRequestContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(mode);
    }

    private sealed class TestEmbeddingProfileResolver(int dimensions, string provider = "onnx", string model = "test-model") : IEmbeddingProfileResolver
    {
        public Task<EmbeddingModelDescriptor> ResolveActiveDescriptorAsync(string tenantId, string appId, string collectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new EmbeddingModelDescriptor(provider, model, dimensions, 512, EmbeddingDistanceMetric.Cosine));
    }
}
