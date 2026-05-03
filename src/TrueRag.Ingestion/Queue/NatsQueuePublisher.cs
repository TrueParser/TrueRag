using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using TrueRag.Ingestion.Configuration;

namespace TrueRag.Ingestion.Queue;

internal sealed class NatsQueuePublisher : IQueuePublisher, IAsyncDisposable
{
    private readonly NatsConnection _connection;
    private readonly NatsJSContext _jsContext;
    private readonly ILogger<NatsQueuePublisher> _logger;
    private readonly string _streamName;
    private readonly string _subjectPrefix;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public NatsQueuePublisher(IConfiguration configuration, ILogger<NatsQueuePublisher> logger)
    {
        _logger = logger;
        var queueSection = configuration.GetSection(QueueConfiguration.SectionName);
        _connection = NatsConnectionFactory.Create(configuration, "TrueRag.API.Publisher");
        _jsContext = new NatsJSContext(_connection);
        _streamName = queueSection.GetValue(nameof(QueueConfiguration.StreamName), "TrueRagJob")!;
        _subjectPrefix = queueSection.GetValue(nameof(QueueConfiguration.SubjectPrefix), "TrueRAG.Job")!;
    }

    public async Task EnsureStreamProvisionedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await NatsJetStreamProvisioner.EnsureStreamAsync(
            _jsContext,
            _streamName,
            _subjectPrefix + ".>",
            "TrueRAG ingestion stream",
            cancellationToken);
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        var ack = await _jsContext.PublishAsync(topic, payload, cancellationToken: cancellationToken);
        ack.EnsureSuccess();

        _logger.LogInformation("Published ingestion job. Topic={Topic} Stream={Stream} Seq={Seq}", topic, ack.Stream, ack.Seq);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection.ConnectionState == NatsConnectionState.Open)
        {
            return;
        }

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection.ConnectionState != NatsConnectionState.Open)
            {
                await _connection.ConnectAsync();
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _connectLock.Dispose();
        await _connection.DisposeAsync();
    }
}