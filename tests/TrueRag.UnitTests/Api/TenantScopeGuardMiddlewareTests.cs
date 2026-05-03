using Microsoft.AspNetCore.Http;
using TrueRag.Api.Context;
using TrueRag.Api.Middleware;

namespace TrueRag.UnitTests.Api;

public sealed class TenantScopeGuardMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RejectsMissingTenantOnApiPath()
    {
        var middleware = new TenantScopeGuardMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/rag/generate";

        await middleware.InvokeAsync(context, new ThrowingResolver());

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_BypassesHealthPath()
    {
        var nextCalled = false;
        var middleware = new TenantScopeGuardMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/health/live";

        await middleware.InvokeAsync(context, new ThrowingResolver());

        Assert.True(nextCalled);
    }

    private sealed class ThrowingResolver : IRequestContextResolver
    {
        public TrueRag.Core.Context.IRequestContext Resolve(HttpContext httpContext)
            => throw new RequestContextResolutionException("missing tenant");
    }
}
