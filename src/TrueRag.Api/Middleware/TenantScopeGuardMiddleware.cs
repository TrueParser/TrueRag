using Microsoft.AspNetCore.Http;
using TrueRag.Api.Context;

namespace TrueRag.Api.Middleware;

public sealed class TenantScopeGuardMiddleware
{
    private readonly RequestDelegate _next;

    public TenantScopeGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRequestContextResolver resolver)
    {
        if (IsGuardedPath(context.Request.Path))
        {
            try
            {
                _ = resolver.Resolve(context);
            }
            catch (RequestContextResolutionException ex)
            {
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
