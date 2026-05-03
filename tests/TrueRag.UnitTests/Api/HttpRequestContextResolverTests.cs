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
            new Claim("sub", "user-1"),
            new Claim("role", "admin"),
            new Claim("allowed_document_group", "legal"),
            new Claim("allowed_document_group", "finance")
        ]));

        var resolved = resolver.Resolve(context);

        Assert.Equal("tenant-a", resolved.TenantId);
        Assert.Equal("app-x", resolved.AppId);
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

        var resolved = resolver.Resolve(context);

        Assert.Equal("tenant-h", resolved.TenantId);
        Assert.Equal("app-h", resolved.AppId);
    }

    [Fact]
    public void Resolve_Throws_WhenTenantMissing()
    {
        var resolver = CreateResolver();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-App-Id"] = "app-only";

        var ex = Assert.Throws<RequestContextResolutionException>(() => resolver.Resolve(context));

        Assert.Contains("tenant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestContextResolver CreateResolver()
    {
        var options = Options.Create(new RequestContextOptions());
        return new HttpRequestContextResolver(options);
    }
}