using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Api.Context;
using TrueRag.Api.Middleware;

namespace TrueRag.IntegrationTests.Api;

public sealed class TenantScopeGuardEnforcementIntegrationTests
{
    [Fact]
    public async Task ApiPath_WithoutScope_IsRejectedWith400()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/search/text";

        await middleware.InvokeAsync(context, new ThrowingResolver(), new AllowAuthorizer());

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task HealthPath_BypassesScopeGuard()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/health/ready";

        await middleware.InvokeAsync(context, new ThrowingResolver(), new AllowAuthorizer());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task ApiPath_MissingCollection_IsRejectedWith400()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/search/text";
        context.Request.Headers["X-Tenant-Id"] = "tenant-a";
        context.Request.Headers["X-App-Id"] = "app-a";

        await middleware.InvokeAsync(context, new MissingCollectionResolver(), new AllowAuthorizer());

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiPath_InvalidCollectionFormat_IsRejectedWith400()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/search/text";
        context.Request.Headers["X-Tenant-Id"] = "tenant-a";
        context.Request.Headers["X-App-Id"] = "app-a";
        context.Request.Headers["X-Collection-Id"] = "invalid collection";

        await middleware.InvokeAsync(context, new InvalidCollectionResolver(), new AllowAuthorizer());

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiPath_CollectionScopeForbidden_IsRejectedWith403()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/search/text";

        await middleware.InvokeAsync(context, new FixedResolver(), new DenyAuthorizer());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private sealed class ThrowingResolver : IRequestContextResolver
    {
        public TrueRag.Core.Context.IRequestContext Resolve(HttpContext httpContext)
            => throw new RequestContextResolutionException("missing");
    }

    private sealed class MissingCollectionResolver : IRequestContextResolver
    {
        public TrueRag.Core.Context.IRequestContext Resolve(HttpContext httpContext)
            => throw new RequestContextResolutionException("Missing required request context field 'collection'.");
    }

    private sealed class InvalidCollectionResolver : IRequestContextResolver
    {
        public TrueRag.Core.Context.IRequestContext Resolve(HttpContext httpContext)
            => throw new RequestContextResolutionException("Invalid request context field 'collection'. Expected pattern: ^[a-zA-Z0-9._:-]{1,128}$");
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
