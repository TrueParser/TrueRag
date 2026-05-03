namespace TrueRag.Api.ResourceGuard;

public interface IResourceMonitor
{
    ResourceSnapshot Current { get; }

    long IncrementActiveRequests();

    long DecrementActiveRequests();
}
