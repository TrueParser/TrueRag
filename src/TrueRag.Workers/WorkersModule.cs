using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrueRag.Core.Abstractions;

namespace TrueRag.Workers;

public static class WorkersModule
{
    public static IServiceCollection AddTrueRagWorkers(this IServiceCollection services)
    {
        services.TryAddSingleton<IIngestionEmbeddingOrchestrator, NoopIngestionEmbeddingOrchestrator>();
        services.AddHostedService<IngestionQueueWorker>();
        services.AddHostedService<IngestionWalReplayService>();
        services.AddHostedService<IngestionWalPruneService>();
        return services;
    }
}
