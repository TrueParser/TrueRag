using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.Embeddings;

public static class EmbeddingsModule
{
    public static IServiceCollection AddTrueRagEmbeddings(this IServiceCollection services)
    {
        services.TryAddSingleton<IOnnxModelProfileRegistry, OnnxModelProfileRegistry>();
        services.TryAddSingleton<IValidateOptions<OnnxProfileSelectionOptions>, OnnxProfileSelectionOptionsValidator>();
        services.TryAddSingleton<IValidateOptions<OpenAiEmbeddingOptions>, OpenAiEmbeddingOptionsValidator>();
        services.TryAddSingleton<IValidateOptions<EmbeddingModeSelectionOptions>, EmbeddingModeSelectionOptionsValidator>();

        services.AddOptions<EmbeddingModeSelectionOptions>()
            .BindConfiguration(EmbeddingModeSelectionOptions.SectionName)
            .ValidateOnStart();

        services.AddOptions<OnnxProfileSelectionOptions>()
            .BindConfiguration(OnnxProfileSelectionOptions.SectionName)
            .ValidateOnStart();

        services.AddOptions<OnnxEmbeddingOptions>()
            .BindConfiguration(OnnxEmbeddingOptions.SectionName)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ProviderName), "Embeddings:Onnx:ProviderName is required.")
            .Validate(static options => options.MaxBatchSize > 0, "Embeddings:Onnx:MaxBatchSize must be greater than zero.")
            .Validate(static options => options.InferenceTimeoutSeconds > 0, "Embeddings:Onnx:InferenceTimeoutSeconds must be greater than zero.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ModelArtifactsPath), "Embeddings:Onnx:ModelArtifactsPath is required.")
            .ValidateOnStart();

        services.AddOptions<OpenAiEmbeddingOptions>()
            .BindConfiguration(OpenAiEmbeddingOptions.SectionName)
            .ValidateOnStart();

        services.TryAddSingleton<OnnxTextPreprocessor>();
        services.TryAddSingleton<OnnxModelArtifactValidator>();
        services.TryAddSingleton<IOnnxEmbeddingExecutor, DeterministicOnnxEmbeddingExecutor>();
        services.TryAddSingleton<OnnxEmbeddingProvider>();
        services.TryAddSingleton<IEmbeddingProvider>(sp => sp.GetRequiredService<OnnxEmbeddingProvider>());

        services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiEmbeddingOptions>>().Value;
            var client = new HttpClient();
            if (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
            {
                client.BaseAddress = new Uri(endpoint.GetLeftPart(UriPartial.Authority));
            }

            return client;
        });
        services.TryAddSingleton<OpenAiEmbeddingProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEmbeddingProvider, OpenAiEmbeddingProvider>());

        services.TryAddSingleton<IEmbeddingProviderRegistry, EmbeddingProviderRegistry>();
        services.TryAddSingleton<IEmbeddingProfileResolver, OnnxEmbeddingProfileResolver>();
        services.TryAddSingleton<IEmbeddingProfileGovernanceService, EmbeddingProfileGovernanceService>();
        services.TryAddSingleton<IIngestionEmbeddingOrchestrator, IngestionEmbeddingOrchestrator>();
        services.TryAddSingleton<ICollectionEmbeddingModeResolver, CollectionEmbeddingModeResolver>();
        services.TryAddSingleton<IQueryEmbeddingGenerator, QueryEmbeddingGenerator>();

        services.AddHostedService<OnnxEmbeddingWarmupService>();
        services.AddHostedService<OnnxProfileDiagnosticsService>();
        services.AddHostedService<EmbeddingExecutionDiagnosticsService>();

        return services;
    }
}

