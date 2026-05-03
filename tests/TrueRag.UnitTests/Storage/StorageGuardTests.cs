using TrueRag.Core.Context;
using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class StorageGuardTests
{
    [Fact]
    public void EnsureScopedContext_Throws_WhenTenantMissing()
    {
        var context = new RequestContext("", "app", "user", [], []);

        Assert.Throws<ArgumentException>(() => StorageGuard.EnsureScopedContext(context));
    }

    [Fact]
    public void EnsureScopedContext_Throws_WhenAppMissing()
    {
        var context = new RequestContext("tenant", "", "user", [], []);

        Assert.Throws<ArgumentException>(() => StorageGuard.EnsureScopedContext(context));
    }

    [Fact]
    public void EnsureScopedContext_DoesNotThrow_WhenValid()
    {
        var context = new RequestContext("tenant", "app", "user", [], []);

        StorageGuard.EnsureScopedContext(context);
    }
}