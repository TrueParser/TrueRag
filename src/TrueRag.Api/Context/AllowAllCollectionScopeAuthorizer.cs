using Microsoft.AspNetCore.Http;
using TrueRag.Core.Context;

namespace TrueRag.Api.Context;

internal sealed class AllowAllCollectionScopeAuthorizer : ICollectionScopeAuthorizer
{
    public ValueTask<bool> IsAllowedAsync(HttpContext httpContext, IRequestContext requestContext, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(true);
}
