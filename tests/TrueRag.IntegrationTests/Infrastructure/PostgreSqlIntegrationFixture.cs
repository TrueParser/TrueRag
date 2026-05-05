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

            CREATE TABLE IF NOT EXISTS embedding_active_profiles (
                tenant_id TEXT NOT NULL,
                app_id TEXT NOT NULL,
                collection_id TEXT NOT NULL,
                provider TEXT NOT NULL,
                model TEXT NOT NULL,
                dimensions INT NOT NULL,
                max_tokens INT NOT NULL,
                distance_metric TEXT NOT NULL,
                version TEXT NULL,
                checksum TEXT NULL,
                activated_at_utc TIMESTAMPTZ NOT NULL,
                PRIMARY KEY (tenant_id, app_id, collection_id)
            );

            CREATE TABLE IF NOT EXISTS embedding_profile_activation_history (
                id BIGSERIAL PRIMARY KEY,
                tenant_id TEXT NOT NULL,
                app_id TEXT NOT NULL,
                collection_id TEXT NOT NULL,
                provider TEXT NOT NULL,
                model TEXT NOT NULL,
                dimensions INT NOT NULL,
                max_tokens INT NOT NULL,
                distance_metric TEXT NOT NULL,
                version TEXT NULL,
                checksum TEXT NULL,
                activated_at_utc TIMESTAMPTZ NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_embedding_profile_history_scope
                ON embedding_profile_activation_history (tenant_id, app_id, collection_id, activated_at_utc DESC);

            CREATE TABLE IF NOT EXISTS embedding_profile_transitions (
                transition_id TEXT PRIMARY KEY,
                tenant_id TEXT NOT NULL,
                app_id TEXT NOT NULL,
                collection_id TEXT NOT NULL,
                source_provider TEXT NULL,
                source_model TEXT NULL,
                source_dimensions INT NULL,
                target_provider TEXT NOT NULL,
                target_model TEXT NOT NULL,
                target_dimensions INT NOT NULL,
                target_max_tokens INT NOT NULL,
                target_distance_metric TEXT NOT NULL,
                target_version TEXT NULL,
                target_checksum TEXT NULL,
                requires_reembedding BOOLEAN NOT NULL,
                reembedding_completed BOOLEAN NOT NULL,
                status TEXT NOT NULL,
                notes TEXT NULL,
                created_at_utc TIMESTAMPTZ NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_embedding_profile_transitions_scope
                ON embedding_profile_transitions (tenant_id, app_id, collection_id, created_at_utc DESC);
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
