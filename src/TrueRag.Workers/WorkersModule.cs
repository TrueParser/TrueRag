using Microsoft.Extensions.DependencyInjection;

namespace TrueRag.Workers;

public static class WorkersModule
{
    public static IServiceCollection AddTrueRagWorkers(this IServiceCollection services)
    {
        services.AddHostedService<IngestionQueueWorker>();
        services.AddHostedService<IngestionWalReplayService>();
        services.AddHostedService<IngestionWalPruneService>();
        return services;
    }
}
