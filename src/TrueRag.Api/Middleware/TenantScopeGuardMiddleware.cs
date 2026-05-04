using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Api.Context;

namespace TrueRag.Api.Middleware;

public sealed class TenantScopeGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestContextOptions _options;
    private readonly ILogger<TenantScopeGuardMiddleware> _logger;

    public TenantScopeGuardMiddleware(
        RequestDelegate next,
        IOptions<RequestContextOptions> options,
        ILogger<TenantScopeGuardMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IRequestContextResolver resolver,
        ICollectionScopeAuthorizer collectionScopeAuthorizer)
    {
        if (IsGuardedPath(context.Request.Path))
        {
            try
            {
                var requestContext = resolver.Resolve(context);
                if (_options.EnableCollectionScopeAuthorization)
                {
                    var allowed = await collectionScopeAuthorizer.IsAllowedAsync(context, requestContext, context.RequestAborted);
                    if (!allowed)
                    {
                        _logger.LogWarning(
                            "Collection scope authorization denied. tenant={TenantId}, app={AppId}, collection={CollectionId}, path={Path}",
                            requestContext.TenantId,
                            requestContext.AppId,
                            requestContext.CollectionId,
                            context.Request.Path);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "collection_scope_forbidden",
                            message = "Requested collection scope is not permitted for this principal."
                        });
                        return;
                    }
                }
            }
            catch (RequestContextResolutionException ex)
            {
                _logger.LogWarning(ex, "Invalid request context for guarded API path {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "invalid_request_context",
                    message = ex.Message
                });
                return;
            }
        }

        await _next(context);
    }

    private static bool IsGuardedPath(PathString path)
        => path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase);
}
