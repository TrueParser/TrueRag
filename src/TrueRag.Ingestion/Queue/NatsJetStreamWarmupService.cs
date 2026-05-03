using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TrueRag.Ingestion.Queue;

internal sealed class NatsJetStreamWarmupService : IHostedService
{
    private readonly NatsQueuePublisher _queuePublisher;
    private readonly ILogger<NatsJetStreamWarmupService> _logger;

    public NatsJetStreamWarmupService(
        NatsQueuePublisher queuePublisher,
        ILogger<NatsJetStreamWarmupService> logger)
    {
        _queuePublisher = queuePublisher;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pre-provisioning JetStream stream for ingestion jobs.");
        await _queuePublisher.EnsureStreamProvisionedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}