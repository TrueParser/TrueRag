using Npgsql;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Storage.Persistence;

namespace TrueRag.Storage;

internal sealed class ConversationRepository : IConversationRepository
{
    private readonly StorageDataSources _dataSources;

    public ConversationRepository(StorageDataSources dataSources)
    {
        _dataSources = dataSources;
    }

    public async Task<Result> AppendMessageAsync(
        IRequestContext requestContext,
        ConversationMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StorageGuard.EnsureScopedContext(requestContext);

            await using var connection = await _dataSources.Write.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO conversation_messages (
                    thread_id, tenant_id, app_id, collection_id, role, message, occurred_at_utc, active_document_id, active_section_path
                )
                VALUES (
                    @thread_id, @tenant_id, @app_id, @collection_id, @role, @message, @occurred_at_utc, @active_document_id, @active_section_path
                );
                """,
                connection);

            command.Parameters.AddWithValue("thread_id", message.ThreadId);
            command.Parameters.AddWithValue("tenant_id", requestContext.TenantId);
            command.Parameters.AddWithValue("app_id", requestContext.AppId);
            command.Parameters.AddWithValue("collection_id", requestContext.CollectionId);
            command.Parameters.AddWithValue("role", message.Role);
            command.Parameters.AddWithValue("message", message.Message);
            command.Parameters.AddWithValue("occurred_at_utc", message.OccurredAtUtc.UtcDateTime);
            command.Parameters.AddWithValue("active_document_id", (object?)message.ActiveDocumentId ?? DBNull.Value);
            command.Parameters.AddWithValue("active_section_path", (object?)message.ActiveSectionPath ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("storage.conversation_append_failed", ex.Message, ErrorType.Unexpected));
        }
    }

    public async Task<Result<IReadOnlyCollection<ConversationMessage>>> GetThreadAsync(
        IRequestContext requestContext,
        string threadId,
        int take,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            await using var connection = await _dataSources.Read.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                SELECT thread_id, role, message, occurred_at_utc, active_document_id, active_section_path
                FROM conversation_messages
                WHERE tenant_id = @tenant_id
                  AND app_id = @app_id
                  AND collection_id = @collection_id
                  AND thread_id = @thread_id
                ORDER BY occurred_at_utc DESC
                LIMIT @take;
                """,
                connection);

            command.Parameters.AddWithValue("tenant_id", requestContext.TenantId);
            command.Parameters.AddWithValue("app_id", requestContext.AppId);
            command.Parameters.AddWithValue("collection_id", requestContext.CollectionId);
            command.Parameters.AddWithValue("thread_id", threadId);
            command.Parameters.AddWithValue("take", Math.Max(1, take));

            var rows = new List<ConversationMessage>(Math.Max(1, take));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new ConversationMessage(
                    ThreadId: reader.GetString(0),
                    Role: reader.GetString(1),
                    Message: reader.GetString(2),
                    OccurredAtUtc: new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)),
                    ActiveDocumentId: reader.IsDBNull(4) ? null : reader.GetString(4),
                    ActiveSectionPath: reader.IsDBNull(5) ? null : reader.GetString(5)));
            }

            rows.Reverse();
            return Result<IReadOnlyCollection<ConversationMessage>>.Success(rows);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyCollection<ConversationMessage>>.Failure(
                new Error("storage.conversation_thread_query_failed", ex.Message, ErrorType.Unexpected));
        }
    }

    public async Task<Result<ConversationThreadState?>> GetThreadStateAsync(
        IRequestContext requestContext,
        string threadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            await using var connection = await _dataSources.Read.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                SELECT thread_id, summary, active_document_id, active_section_path, last_refreshed_at_utc, total_turns
                FROM conversation_thread_states
                WHERE tenant_id = @tenant_id
                  AND app_id = @app_id
                  AND collection_id = @collection_id
                  AND thread_id = @thread_id;
                """,
                connection);

            command.Parameters.AddWithValue("tenant_id", requestContext.TenantId);
            command.Parameters.AddWithValue("app_id", requestContext.AppId);
            command.Parameters.AddWithValue("collection_id", requestContext.CollectionId);
            command.Parameters.AddWithValue("thread_id", threadId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Result<ConversationThreadState?>.Success(null);
            }

            var state = new ConversationThreadState(
                ThreadId: reader.GetString(0),
                Summary: reader.IsDBNull(1) ? null : reader.GetString(1),
                ActiveDocumentId: reader.IsDBNull(2) ? null : reader.GetString(2),
                ActiveSectionPath: reader.IsDBNull(3) ? null : reader.GetString(3),
                LastRefreshedAtUtc: new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)),
                TotalTurns: reader.GetInt32(5));

            return Result<ConversationThreadState?>.Success(state);
        }
        catch (Exception ex)
        {
            return Result<ConversationThreadState?>.Failure(
                new Error("storage.conversation_state_query_failed", ex.Message, ErrorType.Unexpected));
        }
    }

    public async Task<Result> UpsertThreadStateAsync(
        IRequestContext requestContext,
        ConversationThreadState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            await using var connection = await _dataSources.Write.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO conversation_thread_states (
                    thread_id, tenant_id, app_id, collection_id, summary, active_document_id, active_section_path, last_refreshed_at_utc, total_turns
                )
                VALUES (
                    @thread_id, @tenant_id, @app_id, @collection_id, @summary, @active_document_id, @active_section_path, @last_refreshed_at_utc, @total_turns
                )
                ON CONFLICT (thread_id, tenant_id, app_id, collection_id)
                DO UPDATE SET
                    summary = EXCLUDED.summary,
                    active_document_id = EXCLUDED.active_document_id,
                    active_section_path = EXCLUDED.active_section_path,
                    last_refreshed_at_utc = EXCLUDED.last_refreshed_at_utc,
                    total_turns = EXCLUDED.total_turns;
                """,
                connection);

            command.Parameters.AddWithValue("thread_id", state.ThreadId);
            command.Parameters.AddWithValue("tenant_id", requestContext.TenantId);
            command.Parameters.AddWithValue("app_id", requestContext.AppId);
            command.Parameters.AddWithValue("collection_id", requestContext.CollectionId);
            command.Parameters.AddWithValue("summary", (object?)state.Summary ?? DBNull.Value);
            command.Parameters.AddWithValue("active_document_id", (object?)state.ActiveDocumentId ?? DBNull.Value);
            command.Parameters.AddWithValue("active_section_path", (object?)state.ActiveSectionPath ?? DBNull.Value);
            command.Parameters.AddWithValue("last_refreshed_at_utc", state.LastRefreshedAtUtc.UtcDateTime);
            command.Parameters.AddWithValue("total_turns", state.TotalTurns);

            await command.ExecuteNonQueryAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("storage.conversation_state_upsert_failed", ex.Message, ErrorType.Unexpected));
        }
    }
}
