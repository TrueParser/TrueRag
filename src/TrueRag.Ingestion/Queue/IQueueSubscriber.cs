namespace TrueRag.Ingestion.Queue;

public interface IQueueSubscriber
{
    Task SubscribeAsync<T>(
        string topic,
        string consumerGroup,
        Func<T, CancellationToken, Task<bool>> handler,
        CancellationToken cancellationToken = default);
}