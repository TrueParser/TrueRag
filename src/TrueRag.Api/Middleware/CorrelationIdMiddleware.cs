using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using TrueRag.Api.Helpers;

namespace TrueRag.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdAccessor.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        }

        context.Items[CorrelationIdAccessor.ItemKey] = correlationId;
        context.Response.Headers[CorrelationIdAccessor.HeaderName] = correlationId;

        await _next(context);
    }
}
