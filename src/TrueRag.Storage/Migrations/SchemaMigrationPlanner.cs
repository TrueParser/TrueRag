using System.Security.Cryptography;
using System.Text;

namespace TrueRag.Storage.Migrations;

internal static class SchemaMigrationPlanner
{
    public static IReadOnlyCollection<SchemaMigrationDefinition> OrderDeterministically(
        IReadOnlyCollection<SchemaMigrationDefinition> migrations) =>
        migrations
            .OrderBy(static m => m.Version, StringComparer.Ordinal)
            .ToArray();

    public static string ComputeChecksum(string sql)
    {
        var bytes = Encoding.UTF8.GetBytes(sql ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public static IReadOnlyCollection<SchemaMigrationDefinition> BuildPending(
        IReadOnlyCollection<SchemaMigrationDefinition> orderedLocal,
        IReadOnlyCollection<AppliedSchemaMigration> applied)
    {
        var appliedVersions = applied
            .Select(static a => a.Version)
            .ToHashSet(StringComparer.Ordinal);

        return orderedLocal
            .Where(m => !appliedVersions.Contains(m.Version))
            .ToArray();
    }

    public static IReadOnlyCollection<SchemaMigrationDrift> DetectChecksumDrift(
        IReadOnlyCollection<SchemaMigrationDefinition> orderedLocal,
        IReadOnlyCollection<AppliedSchemaMigration> applied)
    {
        var localByVersion = orderedLocal
            .ToDictionary(static m => m.Version, static m => ComputeChecksum(m.Sql), StringComparer.Ordinal);

        var drift = new List<SchemaMigrationDrift>();
        foreach (var entry in applied)
        {
            if (!localByVersion.TryGetValue(entry.Version, out var expected))
            {
                continue;
            }

            if (!string.Equals(expected, entry.Checksum, StringComparison.Ordinal))
            {
                drift.Add(new SchemaMigrationDrift(entry.Version, expected, entry.Checksum));
            }
        }

        return drift;
    }

    public static bool IsGuardedDdl(string sql)
    {
        var normalized = sql?.ToUpperInvariant() ?? string.Empty;
        if (normalized.Contains("DROP TABLE", StringComparison.Ordinal) ||
            normalized.Contains("DROP INDEX", StringComparison.Ordinal) ||
            normalized.Contains("TRUNCATE TABLE", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized.Contains("CREATE TABLE", StringComparison.Ordinal))
        {
            return normalized.Contains("IF NOT EXISTS", StringComparison.Ordinal);
        }

        if (normalized.Contains("CREATE INDEX", StringComparison.Ordinal))
        {
            return normalized.Contains("IF NOT EXISTS", StringComparison.Ordinal);
        }

        return true;
    }
}
