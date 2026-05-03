using Microsoft.AspNetCore.Http;
using TrueRag.Api.ResourceGuard;

namespace TrueRag.UnitTests.Api;

public sealed class ApiAdmissionGuardTests
{
    [Fact]
    public async Task IsRequestAllowedAsync_ReturnsFalse_WhenNodeIsOverloaded()
    {
        var guard = new ApiAdmissionGuard(new StaticMonitor(new ResourceSnapshot(
            10, 10, 0, 0, 2.0, 10, 2, 100, NodeState.Overloaded, "pressure", DateTime.UtcNow)));
        var context = new DefaultHttpContext();

        var allowed = await guard.IsRequestAllowedAsync(context);

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsRequestAllowedAsync_ReturnsTrue_WhenNodeIsNotOverloaded()
    {
        var guard = new ApiAdmissionGuard(new StaticMonitor(new ResourceSnapshot(
            10, 10, 0, 0, 1.0, 10, 10, 10, NodeState.Degraded, "recovering", DateTime.UtcNow)));
        var context = new DefaultHttpContext();

        var allowed = await guard.IsRequestAllowedAsync(context);

        Assert.True(allowed);
    }

    private sealed class StaticMonitor : IResourceMonitor
    {
        public StaticMonitor(ResourceSnapshot snapshot)
        {
            Current = snapshot;
        }

        public ResourceSnapshot Current { get; }

        public long IncrementActiveRequests() => 0;

        public long DecrementActiveRequests() => 0;
    }
}
