using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.Embeddings;

internal sealed class OnnxEmbeddingWarmupService : IHostedService
{
    private readonly OnnxEmbeddingProvider _provider;
    private readonly IOptions<OnnxEmbeddingOptions> _options;
    private readonly ILogger<OnnxEmbeddingWarmupService> _logger;

    public OnnxEmbeddingWarmupService(
        OnnxEmbeddingProvider provider,
        IOptions<OnnxEmbeddingOptions> options,
        ILogger<OnnxEmbeddingWarmupService> logger)
    {
        _provider = provider;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.EnableWarmup)
        {
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.Value.WarmupTimeoutSeconds)));

        try
        {
            await Task.Run(() => _provider.Warmup(timeoutCts.Token), timeoutCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "ONNX embedding warmup failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
