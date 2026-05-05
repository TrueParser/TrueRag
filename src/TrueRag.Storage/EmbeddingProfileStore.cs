using Npgsql;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;
using TrueRag.Storage.Persistence;

namespace TrueRag.Storage;

internal sealed class EmbeddingProfileStore(StorageDataSources dataSources) : IActiveEmbeddingProfileStore
{
    public async Task<ActiveEmbeddingProfileRecord?> GetActiveAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT tenant_id, app_id, collection_id, provider, model, dimensions, max_tokens, distance_metric, version, checksum, activated_at_utc
            FROM embedding_active_profiles
            WHERE tenant_id = @tenant_id AND app_id = @app_id AND collection_id = @collection_id
            LIMIT 1
            """;

        await using var command = new NpgsqlCommand(sql, await dataSources.Read.OpenConnectionAsync(cancellationToken));
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("app_id", appId);
        command.Parameters.AddWithValue("collection_id", collectionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task UpsertActiveAsync(
        ActiveEmbeddingProfileRecord profile,
        CancellationToken cancellationToken = default)
    {
        const string upsert =
            """
            INSERT INTO embedding_active_profiles (
                tenant_id, app_id, collection_id, provider, model, dimensions, max_tokens, distance_metric, version, checksum, activated_at_utc
            )
            VALUES (
                @tenant_id, @app_id, @collection_id, @provider, @model, @dimensions, @max_tokens, @distance_metric, @version, @checksum, @activated_at_utc
            )
            ON CONFLICT (tenant_id, app_id, collection_id)
            DO UPDATE SET
                provider = EXCLUDED.provider,
                model = EXCLUDED.model,
                dimensions = EXCLUDED.dimensions,
                max_tokens = EXCLUDED.max_tokens,
                distance_metric = EXCLUDED.distance_metric,
                version = EXCLUDED.version,
                checksum = EXCLUDED.checksum,
                activated_at_utc = EXCLUDED.activated_at_utc
            """;

        const string historyInsert =
            """
            INSERT INTO embedding_profile_activation_history (
                tenant_id, app_id, collection_id, provider, model, dimensions, max_tokens, distance_metric, version, checksum, activated_at_utc
            )
            VALUES (
                @tenant_id, @app_id, @collection_id, @provider, @model, @dimensions, @max_tokens, @distance_metric, @version, @checksum, @activated_at_utc
            )
            """;

        await using var connection = await dataSources.Write.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var upsertCommand = new NpgsqlCommand(upsert, connection, transaction))
            {
                Bind(upsertCommand, profile);
                await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var historyCommand = new NpgsqlCommand(historyInsert, connection, transaction))
            {
                Bind(historyCommand, profile);
                await historyCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<ActiveEmbeddingProfileRecord>> GetActivationHistoryAsync(
        string tenantId,
        string appId,
        string collectionId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT tenant_id, app_id, collection_id, provider, model, dimensions, max_tokens, distance_metric, version, checksum, activated_at_utc
            FROM embedding_profile_activation_history
            WHERE tenant_id = @tenant_id AND app_id = @app_id AND collection_id = @collection_id
            ORDER BY activated_at_utc DESC
            LIMIT @take
            """;

        await using var command = new NpgsqlCommand(sql, await dataSources.Read.OpenConnectionAsync(cancellationToken));
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("app_id", appId);
        command.Parameters.AddWithValue("collection_id", collectionId);
        command.Parameters.AddWithValue("take", Math.Max(1, take));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<ActiveEmbeddingProfileRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Map(reader));
        }

        return results;
    }

    public async Task<EmbeddingProfileCompatibilityResult> CheckCompatibilityAsync(
        string tenantId,
        string appId,
        string collectionId,
        string provider,
        string model,
        int dimensions,
        CancellationToken cancellationToken = default)
    {
        var active = await GetActiveAsync(tenantId, appId, collectionId, cancellationToken);
        if (active is null)
        {
            return new EmbeddingProfileCompatibilityResult(true);
        }

        if (active.Dimensions != dimensions)
        {
            return new EmbeddingProfileCompatibilityResult(false, "dimensions_mismatch");
        }

        if (!string.Equals(active.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            return new EmbeddingProfileCompatibilityResult(false, "provider_mismatch");
        }

        if (!string.Equals(active.Model, model, StringComparison.OrdinalIgnoreCase))
        {
            return new EmbeddingProfileCompatibilityResult(false, "model_mismatch");
        }

        return new EmbeddingProfileCompatibilityResult(true);
    }

    public async Task<EmbeddingProfileTransitionRecord> CreateTransitionAsync(
        EmbeddingProfileTransitionProposal proposal,
        ActiveEmbeddingProfileRecord? currentProfile,
        CancellationToken cancellationToken = default)
    {
        var transition = new EmbeddingProfileTransitionRecord(
            Guid.NewGuid().ToString("N"),
            proposal.TenantId,
            proposal.AppId,
            proposal.CollectionId,
            currentProfile?.Provider,
            currentProfile?.Model,
            currentProfile?.Dimensions,
            proposal.TargetProvider,
            proposal.TargetModel,
            proposal.TargetDimensions,
            proposal.TargetMaxTokens,
            proposal.TargetDistanceMetric,
            proposal.TargetVersion,
            proposal.TargetChecksum,
            proposal.RequiresReembedding,
            proposal.ReembeddingCompleted,
            proposal.RequiresReembedding && !proposal.ReembeddingCompleted
                ? EmbeddingProfileTransitionStatus.Proposed
                : EmbeddingProfileTransitionStatus.ReadyForActivation,
            proposal.Notes,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        const string sql =
            """
            INSERT INTO embedding_profile_transitions (
                transition_id, tenant_id, app_id, collection_id,
                source_provider, source_model, source_dimensions,
                target_provider, target_model, target_dimensions, target_max_tokens, target_distance_metric, target_version, target_checksum,
                requires_reembedding, reembedding_completed, status, notes, created_at_utc, updated_at_utc
            )
            VALUES (
                @transition_id, @tenant_id, @app_id, @collection_id,
                @source_provider, @source_model, @source_dimensions,
                @target_provider, @target_model, @target_dimensions, @target_max_tokens, @target_distance_metric, @target_version, @target_checksum,
                @requires_reembedding, @reembedding_completed, @status, @notes, @created_at_utc, @updated_at_utc
            )
            """;

        await using var command = new NpgsqlCommand(sql, await dataSources.Write.OpenConnectionAsync(cancellationToken));
        BindTransition(command, transition);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return transition;
    }

    public async Task<EmbeddingProfileTransitionRecord?> GetTransitionByIdAsync(
        string transitionId,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT transition_id, tenant_id, app_id, collection_id,
                   source_provider, source_model, source_dimensions,
                   target_provider, target_model, target_dimensions, target_max_tokens, target_distance_metric, target_version, target_checksum,
                   requires_reembedding, reembedding_completed, status, notes, created_at_utc, updated_at_utc
            FROM embedding_profile_transitions
            WHERE transition_id = @transition_id
            LIMIT 1
            """;

        await using var command = new NpgsqlCommand(sql, await dataSources.Read.OpenConnectionAsync(cancellationToken));
        command.Parameters.AddWithValue("transition_id", transitionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapTransition(reader);
    }

    public async Task<EmbeddingProfileTransitionRecord?> GetLatestPendingTransitionAsync(
        string tenantId,
        string appId,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT transition_id, tenant_id, app_id, collection_id,
                   source_provider, source_model, source_dimensions,
                   target_provider, target_model, target_dimensions, target_max_tokens, target_distance_metric, target_version, target_checksum,
                   requires_reembedding, reembedding_completed, status, notes, created_at_utc, updated_at_utc
            FROM embedding_profile_transitions
            WHERE tenant_id = @tenant_id AND app_id = @app_id AND collection_id = @collection_id
              AND status IN ('Proposed', 'ReadyForActivation')
            ORDER BY created_at_utc DESC
            LIMIT 1
            """;

        await using var command = new NpgsqlCommand(sql, await dataSources.Read.OpenConnectionAsync(cancellationToken));
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("app_id", appId);
        command.Parameters.AddWithValue("collection_id", collectionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapTransition(reader);
    }

    public async Task UpdateTransitionAsync(
        EmbeddingProfileTransitionRecord transition,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            UPDATE embedding_profile_transitions
            SET source_provider = @source_provider,
                source_model = @source_model,
                source_dimensions = @source_dimensions,
                target_provider = @target_provider,
                target_model = @target_model,
                target_dimensions = @target_dimensions,
                target_max_tokens = @target_max_tokens,
                target_distance_metric = @target_distance_metric,
                target_version = @target_version,
                target_checksum = @target_checksum,
                requires_reembedding = @requires_reembedding,
                reembedding_completed = @reembedding_completed,
                status = @status,
                notes = @notes,
                updated_at_utc = @updated_at_utc
            WHERE transition_id = @transition_id
            """;

        await using var command = new NpgsqlCommand(sql, await dataSources.Write.OpenConnectionAsync(cancellationToken));
        BindTransition(command, transition);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Bind(NpgsqlCommand command, ActiveEmbeddingProfileRecord profile)
    {
        command.Parameters.AddWithValue("tenant_id", profile.TenantId);
        command.Parameters.AddWithValue("app_id", profile.AppId);
        command.Parameters.AddWithValue("collection_id", profile.CollectionId);
        command.Parameters.AddWithValue("provider", profile.Provider);
        command.Parameters.AddWithValue("model", profile.Model);
        command.Parameters.AddWithValue("dimensions", profile.Dimensions);
        command.Parameters.AddWithValue("max_tokens", profile.MaxTokens);
        command.Parameters.AddWithValue("distance_metric", profile.DistanceMetric.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("version", (object?)profile.Version ?? DBNull.Value);
        command.Parameters.AddWithValue("checksum", (object?)profile.Checksum ?? DBNull.Value);
        command.Parameters.AddWithValue("activated_at_utc", profile.ActivatedAtUtc.UtcDateTime);
    }

    private static ActiveEmbeddingProfileRecord Map(NpgsqlDataReader reader)
    {
        var metricValue = reader.GetString(7);
        var metric = Enum.TryParse<EmbeddingDistanceMetric>(metricValue, ignoreCase: true, out var parsed)
            ? parsed
            : EmbeddingDistanceMetric.Cosine;

        return new ActiveEmbeddingProfileRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            metric,
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc)));
    }

    private static void BindTransition(NpgsqlCommand command, EmbeddingProfileTransitionRecord transition)
    {
        command.Parameters.AddWithValue("transition_id", transition.TransitionId);
        command.Parameters.AddWithValue("tenant_id", transition.TenantId);
        command.Parameters.AddWithValue("app_id", transition.AppId);
        command.Parameters.AddWithValue("collection_id", transition.CollectionId);
        command.Parameters.AddWithValue("source_provider", (object?)transition.SourceProvider ?? DBNull.Value);
        command.Parameters.AddWithValue("source_model", (object?)transition.SourceModel ?? DBNull.Value);
        command.Parameters.AddWithValue("source_dimensions", (object?)transition.SourceDimensions ?? DBNull.Value);
        command.Parameters.AddWithValue("target_provider", transition.TargetProvider);
        command.Parameters.AddWithValue("target_model", transition.TargetModel);
        command.Parameters.AddWithValue("target_dimensions", transition.TargetDimensions);
        command.Parameters.AddWithValue("target_max_tokens", transition.TargetMaxTokens);
        command.Parameters.AddWithValue("target_distance_metric", transition.TargetDistanceMetric.ToString());
        command.Parameters.AddWithValue("target_version", (object?)transition.TargetVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("target_checksum", (object?)transition.TargetChecksum ?? DBNull.Value);
        command.Parameters.AddWithValue("requires_reembedding", transition.RequiresReembedding);
        command.Parameters.AddWithValue("reembedding_completed", transition.ReembeddingCompleted);
        command.Parameters.AddWithValue("status", transition.Status.ToString());
        command.Parameters.AddWithValue("notes", (object?)transition.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("created_at_utc", transition.CreatedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("updated_at_utc", transition.UpdatedAtUtc.UtcDateTime);
    }

    private static EmbeddingProfileTransitionRecord MapTransition(NpgsqlDataReader reader)
    {
        Enum.TryParse<EmbeddingDistanceMetric>(reader.GetString(11), ignoreCase: true, out var metric);
        Enum.TryParse<EmbeddingProfileTransitionStatus>(reader.GetString(16), ignoreCase: true, out var status);

        return new EmbeddingProfileTransitionRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetInt32(9),
            reader.GetInt32(10),
            metric,
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.GetBoolean(14),
            reader.GetBoolean(15),
            status,
            reader.IsDBNull(17) ? null : reader.GetString(17),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(18), DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(19), DateTimeKind.Utc)));
    }
}
