using Npgsql;
using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;

namespace TrueRag.IntegrationTests.Infrastructure;

public sealed class PostgreSqlIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _container;

    public PostgreSqlIntegrationFixture()
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

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var sql =
            """
            CREATE TABLE IF NOT EXISTS nodes (
                id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL,
                document_group_id TEXT NOT NULL,
                version_number TEXT NOT NULL,
                tenant_id TEXT NOT NULL,
                app_id TEXT NOT NULL,
                collection_id TEXT NOT NULL,
                allowed_document_groups TEXT[] NOT NULL,
                fidelity_level TEXT NOT NULL,
                parent_id TEXT NULL,
                logical_path TEXT NULL,
                node_type TEXT NOT NULL,
                text TEXT NOT NULL,
                page INT NULL,
                x DOUBLE PRECISION NULL,
                y DOUBLE PRECISION NULL,
                w DOUBLE PRECISION NULL,
                h DOUBLE PRECISION NULL,
                referenced_node_ids TEXT[] NULL,
                vector REAL[] NOT NULL,
                search_vector tsvector GENERATED ALWAYS AS (to_tsvector('english', COALESCE(text, ''))) STORED
            );

            CREATE INDEX IF NOT EXISTS ix_nodes_scope_doc
                ON nodes (tenant_id, app_id, collection_id, document_id);

            CREATE INDEX IF NOT EXISTS ix_nodes_scope_group
                ON nodes (tenant_id, app_id, collection_id, document_group_id);

            CREATE TABLE IF NOT EXISTS conversation_messages (
                id BIGSERIAL PRIMARY KEY,
                thread_id TEXT NOT NULL,
                tenant_id TEXT NOT NULL,
                app_id TEXT NOT NULL,
                collection_id TEXT NOT NULL,
                role TEXT NOT NULL,
                message TEXT NOT NULL,
                occurred_at_utc TIMESTAMPTZ NOT NULL,
                active_document_id TEXT NULL,
                active_section_path TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_conversation_messages_scope
                ON conversation_messages (tenant_id, app_id, collection_id, thread_id, occurred_at_utc DESC);

            CREATE TABLE IF NOT EXISTS conversation_thread_states (
                thread_id TEXT NOT NULL,
                tenant_id TEXT NOT NULL,
                app_id TEXT NOT NULL,
                collection_id TEXT NOT NULL,
                summary TEXT NULL,
                active_document_id TEXT NULL,
                active_section_path TEXT NULL,
                last_refreshed_at_utc TIMESTAMPTZ NOT NULL,
                total_turns INT NOT NULL,
                PRIMARY KEY (thread_id, tenant_id, app_id, collection_id)
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
