using Npgsql;
using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;

namespace TrueRag.IntegrationTests.Infrastructure;

public sealed class RawPostgreSqlIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _container;

    public RawPostgreSqlIntegrationFixture()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:17-alpine")
                .WithDatabase("truerag")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
            IsAvailable = true;
        }
        catch (DockerUnavailableException ex)
        {
            IsAvailable = false;
            UnavailableReason = ex.Message;
        }
    }

    public bool IsAvailable { get; }

    public string? UnavailableReason { get; }

    public string ConnectionString => _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (!IsAvailable || _container is null)
        {
            return;
        }

        try
        {
            await _container.StartAsync();
        }
        catch (DockerUnavailableException)
        {
            return;
        }
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS(
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = @table_name
            );
            """,
            connection);
        command.Parameters.AddWithValue("table_name", tableName);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    public async Task<long> CountAppliedMigrationsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM schema_migrations;", connection);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
