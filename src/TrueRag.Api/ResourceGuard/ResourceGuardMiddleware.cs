using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace TrueRag.Api.ResourceGuard;

public sealed class ResourceGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ResourceGuardOptions _options;

    public ResourceGuardMiddleware(
        RequestDelegate next,
        IOptions<ResourceGuardOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, IResourceMonitor monitor, IApiAdmissionGuard admissionGuard)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        monitor.IncrementActiveRequests();
        try
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (IsBypassed(path))
            {
                await _next(context);
                return;
            }

            if (!await admissionGuard.IsRequestAllowedAsync(context, context.RequestAborted))
            {
                var snapshot = context.Items.TryGetValue("resource_guard_state", out var stateObj) && stateObj is ResourceSnapshot snap
                    ? snap
                    : monitor.Current;
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = _options.RetryAfterOverloadedSeconds.ToString();
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "node_overloaded",
                    message = "Node is overloaded. Please retry later.",
                    state = snapshot.State.ToString().ToLowerInvariant(),
                    reason = snapshot.DegradationReason,
                    retryAfter = _options.RetryAfterOverloadedSeconds
                });
                return;
            }

            await _next(context);
        }
        finally
        {
            monitor.DecrementActiveRequests();
        }
    }

    private bool IsBypassed(string path)
        => _options.BypassPaths.Length > 0 &&
           _options.BypassPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
