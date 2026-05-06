using Npgsql;
using TrueRag.Storage.Persistence;

namespace TrueRag.Storage.Migrations;

internal sealed class SchemaMigrationService : ISchemaMigrationService
{
    private readonly StorageDataSources _dataSources;
    private readonly DatabaseEngine _engine;
    private readonly IReadOnlyCollection<SchemaMigrationDefinition> _orderedMigrations;

    public SchemaMigrationService(StorageDataSources dataSources, DatabaseEngine engine)
    {
        _dataSources = dataSources;
        _engine = engine;
        _orderedMigrations = SchemaMigrationPlanner.OrderDeterministically(SchemaMigrationCatalog.ForEngine(engine));
    }

    public async Task<SchemaMigrationPlan> GetPlanAsync(CancellationToken cancellationToken = default)
    {
        var applied = await LoadAppliedMigrationsAsync(cancellationToken);
        var pending = SchemaMigrationPlanner.BuildPending(_orderedMigrations, applied);
        return new SchemaMigrationPlan(pending, applied);
    }

    public async Task<IReadOnlyCollection<SchemaMigrationDrift>> ValidateChecksumsAsync(CancellationToken cancellationToken = default)
    {
        var applied = await LoadAppliedMigrationsAsync(cancellationToken);
        return SchemaMigrationPlanner.DetectChecksumDrift(_orderedMigrations, applied);
    }

    public async Task<SchemaMigrationPlan> ApplyPendingAsync(CancellationToken cancellationToken = default)
    {
        await EnsureHistoryTableAsync(cancellationToken);

        var plan = await GetPlanAsync(cancellationToken);
        if (plan.Pending.Count == 0)
        {
            return plan;
        }

        await using var connection = await _dataSources.Write.OpenConnectionAsync(cancellationToken);
        foreach (var migration in plan.Pending)
        {
            if (!SchemaMigrationPlanner.IsGuardedDdl(migration.Sql))
            {
                throw new InvalidOperationException(
                    $"Migration {migration.Version} contains non-guarded or destructive DDL and cannot run by default.");
            }

            await using var tx = await connection.BeginTransactionAsync(cancellationToken);
            await using (var apply = new NpgsqlCommand(migration.Sql, connection, tx))
            {
                await apply.ExecuteNonQueryAsync(cancellationToken);
            }

            var checksum = SchemaMigrationPlanner.ComputeChecksum(migration.Sql);
            await using (var record = new NpgsqlCommand(
                             """
                             INSERT INTO schema_migrations (version, checksum, applied_utc)
                             VALUES (@version, @checksum, CURRENT_TIMESTAMP)
                             ON CONFLICT (version) DO NOTHING;
                             """,
                             connection,
                             tx))
            {
                record.Parameters.AddWithValue("version", migration.Version);
                record.Parameters.AddWithValue("checksum", checksum);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }

        return await GetPlanAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<AppliedSchemaMigration>> LoadAppliedMigrationsAsync(CancellationToken cancellationToken)
    {
        await EnsureHistoryTableAsync(cancellationToken);
        await using var connection = await _dataSources.Write.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT version, checksum, applied_utc
            FROM schema_migrations
            ORDER BY version;
            """,
            connection);

        var list = new List<AppliedSchemaMigration>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new AppliedSchemaMigration(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2)));
        }

        return list;
    }

    private async Task EnsureHistoryTableAsync(CancellationToken cancellationToken)
    {
        var sql = _engine == DatabaseEngine.PostgreSql
            ? """
              CREATE TABLE IF NOT EXISTS schema_migrations (
                  version TEXT PRIMARY KEY,
                  checksum TEXT NOT NULL,
                  applied_utc TIMESTAMPTZ NOT NULL
              );
              """
            : """
              CREATE TABLE IF NOT EXISTS schema_migrations (
                  version TEXT PRIMARY KEY,
                  checksum TEXT,
                  applied_utc TIMESTAMP WITH TIME ZONE
              );
              """;

        await using var connection = await _dataSources.Write.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
