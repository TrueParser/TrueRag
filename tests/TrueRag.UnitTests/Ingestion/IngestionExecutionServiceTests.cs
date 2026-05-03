using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion;
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

        var wal = scope.ServiceProvider.GetRequiredService<TestAcceptanceLog>();
        Assert.Equal(1, wal.AppendCalls);
        Assert.Equal("standard", wal.LastSerializedPayload?.Fidelity);
    }

    [Fact]
    public async Task IngestSyncAsync_PersistsDirectlyWithoutQueuePublish()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IIngestionExecutionService>();
        var request = CreateRequest();
        var context = CreateContext();

        var result = await service.IngestSyncAsync(context, request);

        Assert.True(result.IsSuccess);

        var repository = scope.ServiceProvider.GetRequiredService<TestIngestionRepository>();
        Assert.Equal(1, repository.UpsertCalls);
        Assert.Equal("standard", repository.LastDocument?.Fidelity);

        var queue = scope.ServiceProvider.GetRequiredService<TestQueuePublisher>();
        Assert.Empty(queue.Published);
    }

    private static ServiceCollection CreateServices()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

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

        return services;
    }

    private static IngestionRequestDto CreateRequest()
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
            "doc-1",
            "group-1",
            "1",
            ["group-1"],
            "auto",
            [chunk]);
    }

    private static RequestContext CreateContext()
        => new("tenant-1", "app-1", "user-1", ["reader"], ["group-1"]);

    private sealed class TestAcceptanceLog : IIngestionAcceptanceLog
    {
        public int AppendCalls { get; private set; }

        public IngestionRequestDto? LastSerializedPayload { get; private set; }

        public Task<IngestionWalAppendResult> AppendAsync(
            IngestionWalRecordMetadata metadata,
            Stream payload,
            long payloadLength,
            CancellationToken cancellationToken = default)
        {
            AppendCalls++;

            using var memory = new MemoryStream();
            payload.CopyTo(memory);
            LastSerializedPayload = JsonSerializer.Deserialize<IngestionRequestDto>(memory.ToArray());

            return Task.FromResult(new IngestionWalAppendResult(
                metadata.TenantId,
                metadata.AppId,
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
}
