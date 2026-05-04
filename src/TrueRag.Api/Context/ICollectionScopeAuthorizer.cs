using Microsoft.AspNetCore.Http;
using TrueRag.Core.Context;

namespace TrueRag.Api.Context;

public interface ICollectionScopeAuthorizer
{
    ValueTask<bool> IsAllowedAsync(HttpContext httpContext, IRequestContext requestContext, CancellationToken cancellationToken = default);
}
