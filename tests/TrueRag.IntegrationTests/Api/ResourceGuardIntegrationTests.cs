using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TrueRag.Api.ResourceGuard;

namespace TrueRag.IntegrationTests.Api;

public sealed class ResourceGuardIntegrationTests
{
    [Fact]
    public async Task Middleware_Returns429_WhenNodeOverloaded()
    {
        var monitor = new MutableMonitor(new ResourceSnapshot(
            10, 10, 0, 0, 2.0, 100, 10, 1000, NodeState.Overloaded, "pressure", DateTime.UtcNow));
        var middleware = new ResourceGuardMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status204NoContent;
                await Task.CompletedTask;
            },
            Options.Create(new ResourceGuardOptions { Enabled = true, RetryAfterOverloadedSeconds = 7 }));
        var guard = new ApiAdmissionGuard(monitor);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/ingest/async";

        await middleware.InvokeAsync(context, monitor, guard);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("7", context.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task Middleware_AllowsTraffic_AfterRecovery()
    {
        var monitor = new MutableMonitor(new ResourceSnapshot(
            10, 10, 0, 0, 2.0, 100, 10, 1000, NodeState.Overloaded, "pressure", DateTime.UtcNow));
        var nextCalled = false;
        var middleware = new ResourceGuardMiddleware(
            async ctx =>
            {
                nextCalled = true;
                ctx.Response.StatusCode = StatusCodes.Status204NoContent;
                await Task.CompletedTask;
            },
            Options.Create(new ResourceGuardOptions { Enabled = true }));
        var guard = new ApiAdmissionGuard(monitor);

        var overloadedContext = new DefaultHttpContext();
        overloadedContext.Request.Path = "/api/v1/search/text";
        await middleware.InvokeAsync(overloadedContext, monitor, guard);
        Assert.Equal(StatusCodes.Status429TooManyRequests, overloadedContext.Response.StatusCode);

        monitor.Set(new ResourceSnapshot(
            10, 10, 0, 0, 0.8, 10, 20, 2, NodeState.Healthy, null, DateTime.UtcNow));
        var recoveredContext = new DefaultHttpContext();
        recoveredContext.Request.Path = "/api/v1/search/text";
        await middleware.InvokeAsync(recoveredContext, monitor, guard);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status204NoContent, recoveredContext.Response.StatusCode);
    }

    private sealed class MutableMonitor : IResourceMonitor
    {
        private ResourceSnapshot _current;

        public MutableMonitor(ResourceSnapshot snapshot)
        {
            _current = snapshot;
        }

        public ResourceSnapshot Current => _current;

        public void Set(ResourceSnapshot snapshot)
            => _current = snapshot;

        public long IncrementActiveRequests() => 0;

        public long DecrementActiveRequests() => 0;
    }
}
