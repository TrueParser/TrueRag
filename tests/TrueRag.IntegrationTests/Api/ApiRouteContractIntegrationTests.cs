using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Controllers;

namespace TrueRag.IntegrationTests.Api;

public sealed class ApiRouteContractIntegrationTests
{
    [Fact]
    public void ControllerRoutes_RemainBackwardCompatible()
    {
        AssertRoute<IngestionController>("api/v1/ingest");
        AssertRoute<SearchController>("api/v1/search");
        AssertRoute<ConversationsController>("api/v1/conversations/threads/{threadId}");
        AssertRoute<ContextController>("api/v1/context");
        AssertRoute<HealthController>("health");

        AssertActionTemplate<IngestionController>("IngestAsync", "async");
        AssertActionTemplate<IngestionController>("IngestSync", "sync");
        AssertActionTemplate<SearchController>("Vector", "vector");
        AssertActionTemplate<SearchController>("Text", "text");
        AssertActionTemplate<SearchController>("Hybrid", "hybrid");
        AssertAbsoluteActionTemplate<ConversationsController>("Generate", "/api/v1/rag/generate");
    }

    private static void AssertRoute<TController>(string expected)
    {
        var route = typeof(TController).GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal(expected, route!.Template);
    }

    private static void AssertActionTemplate<TController>(string methodName, string expected)
    {
        var method = typeof(TController).GetMethod(methodName);
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        if (post is not null)
        {
            Assert.Equal(expected, post.Template);
            return;
        }

        var get = method.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(get);
        Assert.Equal(expected, get!.Template);
    }

    private static void AssertAbsoluteActionTemplate<TController>(string methodName, string expected)
    {
        var method = typeof(TController).GetMethod(methodName);
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal(expected, post!.Template);
    }
}
