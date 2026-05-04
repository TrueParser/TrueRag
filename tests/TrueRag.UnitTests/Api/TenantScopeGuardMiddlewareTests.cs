using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Api.Context;
using TrueRag.Api.Middleware;

namespace TrueRag.UnitTests.Api;

public sealed class TenantScopeGuardMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RejectsMissingTenantOnApiPath()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/rag/generate";

        await middleware.InvokeAsync(context, new ThrowingResolver(), new AllowAuthorizer());

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_BypassesHealthPath()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/health/live";

        await middleware.InvokeAsync(context, new ThrowingResolver(), new AllowAuthorizer());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Returns403_WhenCollectionScopeForbidden()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/search/text";

        await middleware.InvokeAsync(context, new FixedResolver(), new DenyAuthorizer());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_BypassesCollectionAuthorizer_WhenDisabled()
    {
        var nextCalled = false;
        var middleware = new TenantScopeGuardMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new RequestContextOptions { EnableCollectionScopeAuthorization = false }),
            NullLogger<TenantScopeGuardMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/search/text";

        await middleware.InvokeAsync(context, new FixedResolver(), new DenyAuthorizer());

        Assert.True(nextCalled);
    }

    private sealed class ThrowingResolver : IRequestContextResolver
    {
        public TrueRag.Core.Context.IRequestContext Resolve(HttpContext httpContext)
            => throw new RequestContextResolutionException("missing tenant");
    }

    private sealed class AllowAuthorizer : ICollectionScopeAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(HttpContext httpContext, TrueRag.Core.Context.IRequestContext requestContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);
    }

    private sealed class DenyAuthorizer : ICollectionScopeAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(HttpContext httpContext, TrueRag.Core.Context.IRequestContext requestContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);
    }

    private sealed class FixedResolver : IRequestContextResolver
    {
        public TrueRag.Core.Context.IRequestContext Resolve(HttpContext httpContext)
            => new TrueRag.Core.Context.RequestContext("tenant", "app", "user", ["reader"], ["group"], "collection");
    }

    private static TenantScopeGuardMiddleware CreateMiddleware(RequestDelegate next)
        => new(
            next,
            Options.Create(new RequestContextOptions()),
            NullLogger<TenantScopeGuardMiddleware>.Instance);
}
