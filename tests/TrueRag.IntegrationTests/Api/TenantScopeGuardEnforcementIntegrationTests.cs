using Microsoft.AspNetCore.Http;
using TrueRag.Api.Context;
using TrueRag.Api.Middleware;

namespace TrueRag.IntegrationTests.Api;

public sealed class TenantScopeGuardEnforcementIntegrationTests
{
    [Fact]
    public async Task ApiPath_WithoutScope_IsRejectedWith400()
    {
        var middleware = new TenantScopeGuardMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/search/text";

        await middleware.InvokeAsync(context, new ThrowingResolver());

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task HealthPath_BypassesScopeGuard()
    {
        var nextCalled = false;
        var middleware = new TenantScopeGuardMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/health/ready";

        await middleware.InvokeAsync(context, new ThrowingResolver());

        Assert.True(nextCalled);
    }

    private sealed class ThrowingResolver : IRequestContextResolver
    {
        public TrueRag.Core.Context.IRequestContext Resolve(HttpContext httpContext)
            => throw new RequestContextResolutionException("missing");
    }
}
