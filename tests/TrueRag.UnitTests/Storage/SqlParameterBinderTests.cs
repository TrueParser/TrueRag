using Npgsql;
using TrueRag.Core.Context;
using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class SqlParameterBinderTests
{
    [Fact]
    public void BindContext_WhenAclGroupsMissing_BindsEmptyArrayForDefaultDeny()
    {
        using var command = new NpgsqlCommand();
        var context = new RequestContext("tenant-1", "app-1", "user-1", [], []);

        SqlParameterBinder.BindContext(command, context);

        var acl = Assert.IsType<string[]>(command.Parameters["acl_groups"].Value);
        Assert.Empty(acl);
    }
}
