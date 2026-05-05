using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrueRag.Core.Abstractions;
using TrueRag.Retrieval.Configuration;

namespace TrueRag.Retrieval;

public static class RetrievalModule
{
    public static IServiceCollection AddTrueRagRetrieval(this IServiceCollection services)
    {
        services.AddOptions<RetrievalEngineOptions>()
            .BindConfiguration(RetrievalEngineOptions.SectionName);

        services.TryAddSingleton<IRetrievalSemanticCache, DistributedRetrievalSemanticCache>();
        services.TryAddSingleton<IDistributedRetrievalRateLimitStore, DistributedRetrievalRateLimitStore>();
        services.TryAddSingleton<ICollectionEmbeddingModeResolver, NoopCollectionEmbeddingModeResolver>();
        services.TryAddSingleton<IQueryEmbeddingGenerator, NoopQueryEmbeddingGenerator>();
        services.TryAddScoped<IRetrievalService, RetrievalService>();
        return services;
    }
}
