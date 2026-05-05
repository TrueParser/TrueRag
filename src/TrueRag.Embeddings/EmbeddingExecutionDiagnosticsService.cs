using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.Embeddings;

internal sealed class EmbeddingExecutionDiagnosticsService(
    IOptions<OpenAiEmbeddingOptions> openAiOptions,
    IEmbeddingProviderRegistry providerRegistry,
    IIngestionEmbeddingOrchestrator ingestionEmbeddingOrchestrator,
    ILogger<EmbeddingExecutionDiagnosticsService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var providers = providerRegistry.GetRegisteredProviderNames();
        var openAi = openAiOptions.Value;
        var isNoopOrchestrator = ingestionEmbeddingOrchestrator.GetType().Name.Contains("Noop", StringComparison.OrdinalIgnoreCase);

        logger.LogInformation(
            "Embedding execution diagnostics: providers=[{Providers}], openai_enabled={OpenAiEnabled}, async_orchestrator={OrchestratorType}.",
            string.Join(",", providers),
            openAi.Enabled,
            ingestionEmbeddingOrchestrator.GetType().Name);

        if (isNoopOrchestrator)
        {
            logger.LogWarning(
                "Async embedding execution is running in degraded mode: no production ingestion embedding orchestrator is configured.");
        }

        if (openAi.Enabled && string.IsNullOrWhiteSpace(openAi.ApiKey))
        {
            logger.LogWarning(
                "OpenAI external embedding is enabled but API key is empty. Async external embedding will fail.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

