using TrueRag.Storage.Migrations;

namespace TrueRag.UnitTests.Storage;

public sealed class SchemaMigrationPlannerTests
{
    [Fact]
    public void OrderDeterministically_SortsByVersionOrdinal()
    {
        var unordered = new[]
        {
            new SchemaMigrationDefinition("0003", "three", "SELECT 3;"),
            new SchemaMigrationDefinition("0001", "one", "SELECT 1;"),
            new SchemaMigrationDefinition("0002", "two", "SELECT 2;")
        };

        var ordered = SchemaMigrationPlanner.OrderDeterministically(unordered).ToArray();

        Assert.Equal(["0001", "0002", "0003"], ordered.Select(static m => m.Version).ToArray());
    }

    [Fact]
    public void BuildPending_ExcludesAppliedVersions()
    {
        var ordered = new[]
        {
            new SchemaMigrationDefinition("0001", "one", "SELECT 1;"),
            new SchemaMigrationDefinition("0002", "two", "SELECT 2;")
        };

        var applied = new[]
        {
            new AppliedSchemaMigration("0001", SchemaMigrationPlanner.ComputeChecksum("SELECT 1;"), DateTimeOffset.UtcNow)
        };

        var pending = SchemaMigrationPlanner.BuildPending(ordered, applied).ToArray();

        Assert.Single(pending);
        Assert.Equal("0002", pending[0].Version);
    }

    [Fact]
    public void DetectChecksumDrift_ReturnsMismatchedVersions()
    {
        var ordered = new[]
        {
            new SchemaMigrationDefinition("0001", "one", "SELECT 1;")
        };

        var applied = new[]
        {
            new AppliedSchemaMigration("0001", "BAD_CHECKSUM", DateTimeOffset.UtcNow)
        };

        var drift = SchemaMigrationPlanner.DetectChecksumDrift(ordered, applied).ToArray();

        Assert.Single(drift);
        Assert.Equal("0001", drift[0].Version);
    }

    [Theory]
    [InlineData("CREATE TABLE IF NOT EXISTS t (id INT);", true)]
    [InlineData("CREATE INDEX IF NOT EXISTS idx_t_id ON t (id);", true)]
    [InlineData("CREATE TABLE t (id INT);", false)]
    [InlineData("DROP TABLE IF EXISTS t;", false)]
    public void IsGuardedDdl_EnforcesNonDestructiveGuards(string sql, bool expected)
    {
        Assert.Equal(expected, SchemaMigrationPlanner.IsGuardedDdl(sql));
    }
}
