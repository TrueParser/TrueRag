namespace TrueRag.Ingestion.Queue;

public interface IQueuePublisher
{
    Task PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default);
}