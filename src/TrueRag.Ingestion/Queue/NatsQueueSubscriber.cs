using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TrueRag.Ingestion.Configuration;

namespace TrueRag.Ingestion.Queue;

internal sealed class NatsQueueSubscriber : IQueueSubscriber, IAsyncDisposable
{
    private readonly NatsConnection _connection;
    private readonly NatsJSContext _jsContext;
    private readonly ILogger<NatsQueueSubscriber> _logger;
    private readonly string _streamName;
    private readonly string _subjectPrefix;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public NatsQueueSubscriber(IConfiguration configuration, ILogger<NatsQueueSubscriber> logger)
    {
        _logger = logger;
        var queueSection = configuration.GetSection(QueueConfiguration.SectionName);
        _connection = NatsConnectionFactory.Create(configuration, "TrueRag.Worker.Subscriber");
        _jsContext = new NatsJSContext(_connection);
        _streamName = queueSection.GetValue(nameof(QueueConfiguration.StreamName), "TrueRagJob")!;
        _subjectPrefix = queueSection.GetValue(nameof(QueueConfiguration.SubjectPrefix), "TrueRAG.Job")!;
    }

    public async Task SubscribeAsync<T>(
        string topic,
        string consumerGroup,
        Func<T, CancellationToken, Task<bool>> handler,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await NatsJetStreamProvisioner.EnsureStreamAsync(
            _jsContext,
            _streamName,
            _subjectPrefix + ".>",
            "TrueRAG ingestion stream",
            cancellationToken);

        var consumerConfig = new ConsumerConfig(consumerGroup)
        {
            FilterSubject = topic,
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
            MaxDeliver = 3,
            AckWait = TimeSpan.FromMinutes(10)
        };

        var consumer = await _jsContext.CreateOrUpdateConsumerAsync(_streamName, consumerConfig, cancellationToken);

        await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: cancellationToken))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<T>(msg.Data);
                if (payload is null)
                {
                    await msg.AckAsync(cancellationToken: cancellationToken);
                    continue;
                }

                var success = await handler(payload, cancellationToken);
                if (success)
                {
                    await msg.AckAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    await msg.NakAsync(cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queue consumer failed. Topic={Topic}", topic);
                await msg.NakAsync(cancellationToken: cancellationToken);
            }
        }
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