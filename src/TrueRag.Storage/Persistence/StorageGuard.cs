using TrueRag.Core.Context;

namespace TrueRag.Storage.Persistence;

internal static class StorageGuard
{
    public static void EnsureScopedContext(IRequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestContext.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestContext.AppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestContext.CollectionId);
    }
}
