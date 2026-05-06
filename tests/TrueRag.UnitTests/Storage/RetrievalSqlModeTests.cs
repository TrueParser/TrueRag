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

    [Fact]
    public void CrateDb_HybridSql_UsesRrfFusion()
    {
        var sql = StorageSqlDialect.Create(DatabaseEngine.CrateDb).BuildHybridQuerySql();
        Assert.Contains("1.0 / (50 +", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostgreSql_VectorSql_UsesIndexSafeDistanceOrdering()
    {
        var sql = StorageSqlDialect.Create(DatabaseEngine.PostgreSql).BuildVectorQuerySql();

        Assert.Contains("ORDER BY vector <=> @query_vector", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORDER BY 1 - (vector <=> @query_vector)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostgreSql_TextSql_UsesSearchVectorAndTsRank()
    {
        var sql = StorageSqlDialect.Create(DatabaseEngine.PostgreSql).BuildTextQuerySql();

        Assert.Contains("search_vector @@ websearch_to_tsquery", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ts_rank_cd(search_vector", sql, StringComparison.OrdinalIgnoreCase);
    }
}
