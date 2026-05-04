using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TrueRag.Api.Context;

namespace TrueRag.UnitTests.Api;

public sealed class HttpRequestContextResolverTests
{
    [Fact]
    public void Resolve_UsesClaims_WhenPresent()
    {
        var resolver = CreateResolver();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", "tenant-a"),
            new Claim("app_id", "app-x"),
            new Claim("collection_id", "collection-a"),
            new Claim("sub", "user-1"),
            new Claim("role", "admin"),
            new Claim("allowed_document_group", "legal"),
            new Claim("allowed_document_group", "finance")
        ]));

        var resolved = resolver.Resolve(context);

        Assert.Equal("tenant-a", resolved.TenantId);
        Assert.Equal("app-x", resolved.AppId);
        Assert.Equal("collection-a", resolved.CollectionId);
        Assert.Equal("user-1", resolved.UserId);
        Assert.Contains("admin", resolved.Roles);
        Assert.Contains("legal", resolved.AllowedDocumentGroups);
        Assert.Contains("finance", resolved.AllowedDocumentGroups);
    }

    [Fact]
    public void Resolve_FallsBackToHeaders_WhenClaimsMissing()
    {
        var resolver = CreateResolver();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant-h";
        context.Request.Headers["X-App-Id"] = "app-h";
        context.Request.Headers["X-Collection-Id"] = "collection-h";

        var resolved = resolver.Resolve(context);

        Assert.Equal("tenant-h", resolved.TenantId);
        Assert.Equal("app-h", resolved.AppId);
        Assert.Equal("collection-h", resolved.CollectionId);
    }

    [Fact]
    public void Resolve_Throws_WhenTenantMissing()
    {
        var resolver = CreateResolver();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-App-Id"] = "app-only";
        context.Request.Headers["X-Collection-Id"] = "collection-only";

        var ex = Assert.Throws<RequestContextResolutionException>(() => resolver.Resolve(context));

        Assert.Contains("tenant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_Throws_WhenCollectionFormatInvalid()
    {
        var resolver = CreateResolver();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant-h";
        context.Request.Headers["X-App-Id"] = "app-h";
        context.Request.Headers["X-Collection-Id"] = "collection with spaces";

        var ex = Assert.Throws<RequestContextResolutionException>(() => resolver.Resolve(context));

        Assert.Contains("collection", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pattern", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestContextResolver CreateResolver()
    {
        var options = Options.Create(new RequestContextOptions());
        return new HttpRequestContextResolver(options);
    }
}
