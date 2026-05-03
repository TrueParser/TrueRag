using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Api;
using TrueRag.Core.Context;

namespace TrueRag.UnitTests.Api;

public sealed class ApiModuleRegistrationTests
{
    [Fact]
    public void AddTrueRagApi_RegistersScopedRequestContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build());
        services.AddTrueRagApi();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Request.Headers["X-Tenant-Id"] = "tenant";
        accessor.HttpContext.Request.Headers["X-App-Id"] = "app";

        var context = scope.ServiceProvider.GetRequiredService<IRequestContext>();

        Assert.Equal("tenant", context.TenantId);
        Assert.Equal("app", context.AppId);
    }
}