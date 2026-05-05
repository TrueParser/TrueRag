using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrueRag.Api.Services;

namespace TrueRag.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddTrueRagApiServices(this IServiceCollection services)
    {
        services.TryAddScoped<IIngestionApiService, IngestionApiService>();
        services.TryAddScoped<IRetrievalApiService, RetrievalApiService>();
        services.TryAddScoped<IConversationApiService, ConversationApiService>();
        services.TryAddScoped<IEmbeddingProfileApiService, EmbeddingProfileApiService>();
        services.TryAddScoped<IDependencyReadinessEvaluator, DependencyReadinessEvaluator>();
        return services;
    }
}
