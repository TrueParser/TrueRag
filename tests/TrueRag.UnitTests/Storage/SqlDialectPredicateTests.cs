using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class SqlDialectPredicateTests
{
    [Theory]
    [InlineData(DatabaseEngine.CrateDb)]
    [InlineData(DatabaseEngine.PostgreSql)]
    public void AllQuerySql_IncludesTenantAppCollectionAndAclPredicates(DatabaseEngine engine)
    {
        var dialect = StorageSqlDialect.Create(engine);

        Assert.Contains("tenant_id = @tenant_id", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app_id = @app_id", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("collection_id = @collection_id", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowed_document_groups", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required_fidelity", dialect.BuildVectorQuerySql(), StringComparison.OrdinalIgnoreCase);

        Assert.Contains("tenant_id = @tenant_id", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app_id = @app_id", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("collection_id = @collection_id", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowed_document_groups", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required_fidelity", dialect.BuildTextQuerySql(), StringComparison.OrdinalIgnoreCase);

        Assert.Contains("tenant_id = @tenant_id", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app_id = @app_id", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("collection_id = @collection_id", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowed_document_groups", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required_fidelity", dialect.BuildHybridQuerySql(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabaseEngine.CrateDb)]
    [InlineData(DatabaseEngine.PostgreSql)]
    public void UpsertSql_IncludesCollectionColumnAndParameter(DatabaseEngine engine)
    {
        var sql = StorageSqlDialect.Create(engine).BuildUpsertSql();

        Assert.Contains("collection_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@collection_id", sql, StringComparison.OrdinalIgnoreCase);
    }
}
