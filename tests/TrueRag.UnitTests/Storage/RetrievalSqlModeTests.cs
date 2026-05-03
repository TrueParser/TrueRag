using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class RetrievalSqlModeTests
{
    [Fact]
    public void CrateDb_VectorSql_UsesKnnMatch()
    {
        var sql = StorageSqlDialect.Create(DatabaseEngine.CrateDb).BuildVectorQuerySql();
        Assert.Contains("knn_match", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrateDb_TextSql_UsesMatch()
    {
        var sql = StorageSqlDialect.Create(DatabaseEngine.CrateDb).BuildTextQuerySql();
        Assert.Contains("MATCH(text, @query_text)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabaseEngine.CrateDb)]
    [InlineData(DatabaseEngine.PostgreSql)]
    public void HybridSql_UsesRrfFusion(DatabaseEngine engine)
    {
        var sql = StorageSqlDialect.Create(engine).BuildHybridQuerySql();
        Assert.Contains("1.0 / (50 +", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
    }
}