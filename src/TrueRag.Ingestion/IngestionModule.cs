using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrueRag.Core.Models;
using TrueRag.Ingestion.Adapters;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Execution;
using TrueRag.Ingestion.Normalization;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;

namespace TrueRag.Ingestion;

public static class IngestionModule
{
    public static IServiceCollection AddTrueRagIngestion(this IServiceCollection services)
    {
        services.AddOptions<IngestionRuntimeOptions>()
            .BindConfiguration(IngestionRuntimeOptions.SectionName)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.NodeId), "IngestionRuntime.NodeId is required.")
            .Validate(static options => options.SyncMaxConcurrency > 0, "IngestionRuntime.SyncMaxConcurrency must be greater than zero.")
            .Validate(static options => options.WalMaxSegmentBytes >= 1024 * 1024, "IngestionRuntime.WalMaxSegmentBytes must be at least 1MB.");

        services.AddOptions<QueueConfiguration>()
            .BindConfiguration(QueueConfiguration.SectionName)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.SubjectPrefix), "Queue.SubjectPrefix is required.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.StreamName), "Queue.StreamName is required.");

        services.AddOptions<IngestionFidelityOptions>()
            .BindConfiguration(IngestionFidelityOptions.SectionName);

        services.TryAddScoped<IIngestionPayloadAdapter<IngestionRequestDto>, CanonicalIngestionPayloadAdapter>();
        services.TryAddScoped<IIngestionNormalizer, IngestionNormalizer>();

        services.TryAddSingleton<NatsQueuePublisher>();
        services.TryAddSingleton<NatsQueueSubscriber>();
        services.TryAddSingleton<IQueuePublisher>(sp => sp.GetRequiredService<NatsQueuePublisher>());
        services.TryAddSingleton<IQueueSubscriber>(sp => sp.GetRequiredService<NatsQueueSubscriber>());
        services.TryAddSingleton<IWalReadLeaseTracker, WalReadLeaseTracker>();
        services.TryAddSingleton<IIngestionAcceptanceLog, IngestionAcceptanceLog>();
        services.TryAddSingleton<IIngestionWalReader, IngestionWalReader>();
        services.TryAddScoped<IIngestionExecutionService, IngestionExecutionService>();
        services.AddHostedService<NatsJetStreamWarmupService>();
        return services;
    }
}
