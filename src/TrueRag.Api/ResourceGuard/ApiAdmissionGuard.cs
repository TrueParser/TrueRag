using Microsoft.AspNetCore.Http;

namespace TrueRag.Api.ResourceGuard;

internal sealed class ApiAdmissionGuard : IApiAdmissionGuard
{
    private readonly IResourceMonitor _resourceMonitor;

    public ApiAdmissionGuard(IResourceMonitor resourceMonitor)
    {
        _resourceMonitor = resourceMonitor;
    }

    public ValueTask<bool> IsRequestAllowedAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var state = _resourceMonitor.Current;
        context.Items["resource_guard_state"] = state;
        return ValueTask.FromResult(state.State != NodeState.Overloaded);
    }
}
