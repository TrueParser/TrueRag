using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrueRag.Core.Context;
using TrueRag.Core.Models;

namespace TrueRag.Retrieval;

internal interface IRetrievalSemanticCache
{
    Task<RetrievalResponse?> GetAsync(IRequestContext requestContext, string lane, RetrievalQuery query, CancellationToken cancellationToken = default);

    Task SetAsync(IRequestContext requestContext, string lane, RetrievalQuery query, RetrievalResponse response, TimeSpan ttl, CancellationToken cancellationToken = default);
}

internal sealed class DistributedRetrievalSemanticCache : IRetrievalSemanticCache
{
    private readonly IDistributedCache _cache;

    public DistributedRetrievalSemanticCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<RetrievalResponse?> GetAsync(IRequestContext requestContext, string lane, RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        var payload = await _cache.GetStringAsync(BuildKey(requestContext, lane, query), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RetrievalResponse>(payload);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAsync(IRequestContext requestContext, string lane, RetrievalQuery query, RetrievalResponse response, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(response);
        await _cache.SetStringAsync(
            BuildKey(requestContext, lane, query),
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);
    }

    private static string BuildKey(IRequestContext requestContext, string lane, RetrievalQuery query)
    {
        var normalized = JsonSerializer.Serialize(new
        {
            lane,
            q = query.QueryText,
            vector = query.QueryVector,
            topK = query.TopK,
            filters = query.Filters?.OrderBy(static x => x.Key, StringComparer.Ordinal).ToArray()
        });

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return $"retrieval:semantic:{requestContext.TenantId}:{requestContext.AppId}:{requestContext.CollectionId}:{hash}";
    }
}
