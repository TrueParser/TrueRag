using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using TrueRag.Core.Context;

namespace TrueRag.Retrieval;

internal interface IDistributedRetrievalRateLimitStore
{
    Task<bool> TryAcquireAsync(IRequestContext requestContext, string lane, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default);
}

internal sealed class DistributedRetrievalRateLimitStore : IDistributedRetrievalRateLimitStore
{
    private readonly IDistributedCache _cache;

    public DistributedRetrievalRateLimitStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> TryAcquireAsync(IRequestContext requestContext, string lane, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var key = $"retrieval:ratelimit:{requestContext.TenantId}:{requestContext.AppId}:{lane}";
        var now = DateTimeOffset.UtcNow;

        var currentPayload = await _cache.GetStringAsync(key, cancellationToken);
        var current = string.IsNullOrWhiteSpace(currentPayload)
            ? new RateLimitWindow(1, now)
            : JsonSerializer.Deserialize<RateLimitWindow>(currentPayload) ?? new RateLimitWindow(1, now);

        if (now - current.WindowStartUtc >= window)
        {
            current = new RateLimitWindow(1, now);
        }
        else if (current.Count >= maxRequests)
        {
            return false;
        }
        else
        {
            current = current with { Count = current.Count + 1 };
        }

        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(current),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = window },
            cancellationToken);

        return true;
    }

    private sealed record RateLimitWindow(int Count, DateTimeOffset WindowStartUtc);
}
