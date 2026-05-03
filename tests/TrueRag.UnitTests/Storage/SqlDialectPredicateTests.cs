using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class SqlDialectPredicateTests
{
    [Theory]
    [InlineData(DatabaseEngine.CrateDb)]
    [InlineData(DatabaseEngine.PostgreSql)]
    public void AllQuerySql_IncludesTenantAppAndAclPredicates(DatabaseEngine engine)
    {
        var dialect = StorageSqlDialect.Create(engine);

        Assert.Contains("tenant_id = @tenant_id", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app_id = @app_id", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowed_document_groups", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required_fidelity", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);

        Assert.Contains("tenant_id = @tenant_id", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app_id = @app_id", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowed_document_groups", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required_fidelity", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);

        Assert.Contains("tenant_id = @tenant_id", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app_id = @app_id", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowed_document_groups", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required_fidelity", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
    }
}
