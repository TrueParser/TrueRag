using TrueRag.Storage.Migrations;
using TrueRag.Storage.Persistence;

namespace TrueRag.UnitTests.Storage;

public sealed class SchemaMigrationCatalogCompatibilityTests
{
    [Theory]
    [InlineData(DatabaseEngine.CrateDb)]
    [InlineData(DatabaseEngine.PostgreSql)]
    public void Catalog_DefinesCoreTables_ForBothEngines(DatabaseEngine engine)
    {
        var migration = Assert.Single(SchemaMigrationCatalog.ForEngine(engine));
        var sql = migration.Sql;

        Assert.Contains("CREATE TABLE IF NOT EXISTS nodes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS conversation_messages", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS conversation_thread_states", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS embedding_active_profiles", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS embedding_profile_activation_history", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS embedding_profile_transitions", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostgreSqlCatalog_UsesPostgresSpecificSearchAndIndexes()
    {
        var sql = Assert.Single(SchemaMigrationCatalog.ForEngine(DatabaseEngine.PostgreSql)).Sql;

        Assert.Contains("search_vector tsvector", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("USING GIN (search_vector)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("USING GIN (allowed_document_groups)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrateDbCatalog_UsesCrateSpecificTextAndIdentityShapes()
    {
        var sql = Assert.Single(SchemaMigrationCatalog.ForEngine(DatabaseEngine.CrateDb)).Sql;

        Assert.Contains("INDEX USING FULLTEXT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LONG GENERATED ALWAYS AS IDENTITY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabaseEngine.CrateDb)]
    [InlineData(DatabaseEngine.PostgreSql)]
    public void Catalog_Migrations_AreAppendOnlyVersionedAndOrdered(DatabaseEngine engine)
    {
        var ordered = SchemaMigrationPlanner.OrderDeterministically(SchemaMigrationCatalog.ForEngine(engine)).ToArray();

        Assert.NotEmpty(ordered);
        Assert.All(ordered, static m => Assert.Matches(@"^\d{4}$", m.Version));
        Assert.Equal(ordered.Select(static m => m.Version).OrderBy(static v => v, StringComparer.Ordinal), ordered.Select(static m => m.Version));
    }
}
