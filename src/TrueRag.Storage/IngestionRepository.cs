using Npgsql;
using NpgsqlTypes;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Storage.Persistence;

namespace TrueRag.Storage;

internal sealed class IngestionRepository : IIngestionRepository
{
    private readonly StorageDataSources _dataSources;
    private readonly StorageSqlDialect _dialect;

    public IngestionRepository(StorageDataSources dataSources, StorageSqlDialect dialect)
    {
        _dataSources = dataSources;
        _dialect = dialect;
    }

    public async Task<Result> UpsertDocumentAsync(
        IRequestContext requestContext,
        IngestionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            if (request.AllowedDocumentGroups.Count == 0 ||
                request.AllowedDocumentGroups.All(static group => string.IsNullOrWhiteSpace(group)))
            {
                return Result.Failure(new Error(
                    "storage.allowed_document_groups_required",
                    "AllowedDocumentGroups is required and must contain at least one non-empty group.",
                    ErrorType.Validation));
            }

            await using var connection = await _dataSources.Write.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            foreach (var chunk in request.Chunks)
            {
                await using var command = new NpgsqlCommand(_dialect.BuildUpsertSql(), connection, transaction);
                command.Parameters.AddWithValue("id", chunk.Id);
                command.Parameters.AddWithValue("document_id", request.DocumentId);
                command.Parameters.AddWithValue("document_group_id", request.DocumentGroupId);
                command.Parameters.AddWithValue("version_number", request.VersionNumber);
                command.Parameters.AddWithValue("tenant_id", requestContext.TenantId);
                command.Parameters.AddWithValue("app_id", requestContext.AppId);
                command.Parameters.AddWithValue("allowed_document_groups", NpgsqlDbType.Array | NpgsqlDbType.Text, request.AllowedDocumentGroups.ToArray());
                command.Parameters.AddWithValue("fidelity_level", request.Fidelity);
                command.Parameters.AddWithValue("parent_id", (object?)chunk.ParentId ?? DBNull.Value);
                command.Parameters.AddWithValue("logical_path", (object?)chunk.LogicalPath ?? DBNull.Value);
                command.Parameters.AddWithValue("node_type", chunk.Type);
                command.Parameters.AddWithValue("text", chunk.Text);
                command.Parameters.AddWithValue("page", (object?)chunk.BoundingBox?.Page ?? DBNull.Value);
                command.Parameters.AddWithValue("x", (object?)chunk.BoundingBox?.X ?? DBNull.Value);
                command.Parameters.AddWithValue("y", (object?)chunk.BoundingBox?.Y ?? DBNull.Value);
                command.Parameters.AddWithValue("w", (object?)chunk.BoundingBox?.W ?? DBNull.Value);
                command.Parameters.AddWithValue("h", (object?)chunk.BoundingBox?.H ?? DBNull.Value);
                command.Parameters.AddWithValue(
                    "referenced_node_ids",
                    chunk.ReferencedNodeIds is { Count: > 0 }
                        ? chunk.ReferencedNodeIds.ToArray()
                        : DBNull.Value);
                command.Parameters.AddWithValue("vector", NpgsqlDbType.Array | NpgsqlDbType.Real, chunk.Vector);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("storage.upsert_failed", ex.Message, ErrorType.Unexpected));
        }
    }
}
