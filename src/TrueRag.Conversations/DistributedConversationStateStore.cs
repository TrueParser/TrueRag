using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TrueRag.Core.Context;

namespace TrueRag.Conversations;

internal sealed class DistributedConversationStateStore : IConversationStateStore
{
    private static readonly DistributedCacheEntryOptions CacheTtl = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    private readonly IDistributedCache _cache;

    public DistributedConversationStateStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task SetAsync(
        IRequestContext requestContext,
        string threadId,
        string? activeDocumentId,
        string? activeSectionPath,
        CancellationToken cancellationToken = default)
    {
        var state = new ConversationEphemeralState(activeDocumentId, activeSectionPath, DateTimeOffset.UtcNow);
        var payload = JsonSerializer.Serialize(state);
        await _cache.SetStringAsync(BuildKey(requestContext, threadId), payload, CacheTtl, cancellationToken);
    }

    public async Task<ConversationEphemeralState?> GetAsync(
        IRequestContext requestContext,
        string threadId,
        CancellationToken cancellationToken = default)
    {
        var payload = await _cache.GetStringAsync(BuildKey(requestContext, threadId), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ConversationEphemeralState>(payload);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildKey(IRequestContext requestContext, string threadId)
        => $"conversation:state:{requestContext.TenantId}:{requestContext.AppId}:{threadId}";
}
