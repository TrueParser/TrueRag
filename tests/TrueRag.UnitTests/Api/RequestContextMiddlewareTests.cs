using Microsoft.AspNetCore.Http;
using TrueRag.Api.Context;

namespace TrueRag.UnitTests.Api;

public sealed class RequestContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsBadRequest_ForInvalidApiContext()
    {
        var middleware = new RequestContextMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/search/vector";
        var resolver = new ThrowingResolver();

        await middleware.InvokeAsync(httpContext, resolver);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_PassesThrough_ForNonApiPath()
    {
        var nextCalled = false;
        var middleware = new RequestContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/";

        await middleware.InvokeAsync(httpContext, new ThrowingResolver());

        Assert.True(nextCalled);
    }

    private sealed class ThrowingResolver : IRequestContextResolver
    {
        public TrueRag.Core.Context.IRequestContext Resolve(HttpContext httpContext)
        {
            throw new RequestContextResolutionException("invalid");
        }
    }
}