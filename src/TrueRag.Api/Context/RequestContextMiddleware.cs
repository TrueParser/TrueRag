using Microsoft.AspNetCore.Http;

namespace TrueRag.Api.Context;

public sealed class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRequestContextResolver resolver)
    {
        if (IsApiPath(context.Request.Path))
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

    private static bool IsApiPath(PathString path)
    {
        return path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase);
    }
}