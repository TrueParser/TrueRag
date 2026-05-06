using TrueRag.Storage.Persistence;

namespace TrueRag.Storage.Migrations;

internal static class SchemaMigrationCatalog
{
    public static IReadOnlyCollection<SchemaMigrationDefinition> ForEngine(DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => PostgreSqlMigrations,
            DatabaseEngine.CrateDb => CrateDbMigrations,
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported database engine.")
        };

    private static readonly IReadOnlyCollection<SchemaMigrationDefinition> PostgreSqlMigrations =
    [
        new(
            "0001",
            "Create core tables and indexes",
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
                vector REAL[] NULL,
                search_vector tsvector GENERATED ALWAYS AS (to_tsvector('english', COALESCE(text, ''))) STORED
            );

            CREATE INDEX IF NOT EXISTS ix_nodes_scope_doc
                ON nodes (tenant_id, app_id, collection_id, document_id);

            CREATE INDEX IF NOT EXISTS ix_nodes_scope_group
                ON nodes (tenant_id, app_id, collection_id, document_group_id);

            CREATE INDEX IF NOT EXISTS ix_nodes_allowed_document_groups
                ON nodes USING GIN (allowed_document_groups);

            CREATE INDEX IF NOT EXISTS ix_nodes_search_vector
                ON nodes USING GIN (search_vector);

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
            """)
    ];

    private static readonly IReadOnlyCollection<SchemaMigrationDefinition> CrateDbMigrations =
    [
        new(
            "0001",
            "Create core tables and indexes",
            """
            CREATE TABLE IF NOT EXISTS nodes (
                id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL,
                document_group_id TEXT,
                version_number TEXT,
                tenant_id TEXT NOT NULL,
                app_id TEXT NOT NULL,
                collection_id TEXT NOT NULL,
                allowed_document_groups ARRAY(TEXT),
                fidelity_level TEXT NOT NULL,
                parent_id TEXT,
                logical_path TEXT,
                node_type TEXT,
                text TEXT NOT NULL INDEX USING FULLTEXT,
                page INTEGER,
                x DOUBLE,
                y DOUBLE,
                w DOUBLE,
                h DOUBLE,
                referenced_node_ids ARRAY(TEXT),
                vector REAL[]
            );

            CREATE TABLE IF NOT EXISTS conversation_messages (
                id LONG GENERATED ALWAYS AS IDENTITY,
                thread_id TEXT,
                tenant_id TEXT,
                app_id TEXT,
                collection_id TEXT,
                role TEXT,
                message TEXT,
                occurred_at_utc TIMESTAMP WITH TIME ZONE,
                active_document_id TEXT,
                active_section_path TEXT,
                PRIMARY KEY (id)
            );

            CREATE TABLE IF NOT EXISTS conversation_thread_states (
                thread_id TEXT,
                tenant_id TEXT,
                app_id TEXT,
                collection_id TEXT,
                summary TEXT,
                active_document_id TEXT,
                active_section_path TEXT,
                last_refreshed_at_utc TIMESTAMP WITH TIME ZONE,
                total_turns INTEGER,
                PRIMARY KEY (thread_id, tenant_id, app_id, collection_id)
            );

            CREATE TABLE IF NOT EXISTS embedding_active_profiles (
                tenant_id TEXT,
                app_id TEXT,
                collection_id TEXT,
                provider TEXT,
                model TEXT,
                dimensions INTEGER,
                max_tokens INTEGER,
                distance_metric TEXT,
                version TEXT,
                checksum TEXT,
                activated_at_utc TIMESTAMP WITH TIME ZONE,
                PRIMARY KEY (tenant_id, app_id, collection_id)
            );

            CREATE TABLE IF NOT EXISTS embedding_profile_activation_history (
                id LONG GENERATED ALWAYS AS IDENTITY,
                tenant_id TEXT,
                app_id TEXT,
                collection_id TEXT,
                provider TEXT,
                model TEXT,
                dimensions INTEGER,
                max_tokens INTEGER,
                distance_metric TEXT,
                version TEXT,
                checksum TEXT,
                activated_at_utc TIMESTAMP WITH TIME ZONE,
                PRIMARY KEY (id)
            );

            CREATE TABLE IF NOT EXISTS embedding_profile_transitions (
                transition_id TEXT,
                tenant_id TEXT,
                app_id TEXT,
                collection_id TEXT,
                source_provider TEXT,
                source_model TEXT,
                source_dimensions INTEGER,
                target_provider TEXT,
                target_model TEXT,
                target_dimensions INTEGER,
                target_max_tokens INTEGER,
                target_distance_metric TEXT,
                target_version TEXT,
                target_checksum TEXT,
                requires_reembedding BOOLEAN,
                reembedding_completed BOOLEAN,
                status TEXT,
                notes TEXT,
                created_at_utc TIMESTAMP WITH TIME ZONE,
                updated_at_utc TIMESTAMP WITH TIME ZONE,
                PRIMARY KEY (transition_id)
            );
            """)
    ];
}
