using Npgsql;
using NpgsqlTypes;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Storage.Persistence;

namespace TrueRag.Storage;

internal sealed class RetrievalRepository : IRetrievalRepository
{
    private const string SectionExpansionSql =
        """
        SELECT id, document_id, node_type, text, 1.0 AS _score, fidelity_level, page, x, y, w, h, logical_path, document_group_id, version_number, referenced_node_ids
        FROM nodes
        WHERE tenant_id = @tenant_id
          AND app_id = @app_id
          AND (@acl_groups IS NULL OR allowed_document_groups && @acl_groups)
          AND (@required_fidelity IS NULL OR fidelity_level = @required_fidelity)
          AND document_id = @document_id
          AND logical_path LIKE @section_prefix
        LIMIT @section_limit;
        """;

    private const string AdjacentExpansionSql =
        """
        SELECT id, document_id, node_type, text, 1.0 AS _score, fidelity_level, page, x, y, w, h, logical_path, document_group_id, version_number, referenced_node_ids
        FROM nodes
        WHERE tenant_id = @tenant_id
          AND app_id = @app_id
          AND (@acl_groups IS NULL OR allowed_document_groups && @acl_groups)
          AND (@required_fidelity IS NULL OR fidelity_level = @required_fidelity)
          AND document_id = @document_id
          AND id <> @anchor_id
        ORDER BY id
        LIMIT @adjacent_limit;
        """;

    private const string SelectByIdsSql =
        """
        SELECT id, document_id, node_type, text, 1.0 AS _score, fidelity_level, page, x, y, w, h, logical_path, document_group_id, version_number, referenced_node_ids
        FROM nodes
        WHERE tenant_id = @tenant_id
          AND app_id = @app_id
          AND (@acl_groups IS NULL OR allowed_document_groups && @acl_groups)
          AND id = ANY(@node_ids);
        """;

    private const string SelectDiffNodeSql =
        """
        SELECT id, document_id, node_type, text, 1.0 AS _score, fidelity_level, page, x, y, w, h, logical_path, document_group_id, version_number, referenced_node_ids
        FROM nodes
        WHERE tenant_id = @tenant_id
          AND app_id = @app_id
          AND (@acl_groups IS NULL OR allowed_document_groups && @acl_groups)
          AND document_group_id = @document_group_id
          AND version_number = @version_number
          AND logical_path = @logical_path
        LIMIT 1;
        """;

    private readonly StorageDataSources _dataSources;
    private readonly StorageSqlDialect _dialect;

    public RetrievalRepository(StorageDataSources dataSources, StorageSqlDialect dialect)
    {
        _dataSources = dataSources;
        _dialect = dialect;
    }

    public Task<Result<RetrievalResponse>> QueryVectorAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.QueryVector is null || query.QueryVector.Length == 0)
        {
            return Task.FromResult(Result<RetrievalResponse>.Failure(
                new Error("storage.query_vector_missing", "Vector query requires QueryVector.", ErrorType.Validation)));
        }

        return ExecuteQueryAsync(_dialect.BuildVectorQuerySql(), requestContext, query, cancellationToken);
    }

    public Task<Result<RetrievalResponse>> QueryTextAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default) =>
        ExecuteQueryAsync(_dialect.BuildTextQuerySql(), requestContext, query, cancellationToken);

    public Task<Result<RetrievalResponse>> QueryHybridAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.QueryVector is null || query.QueryVector.Length == 0)
        {
            return Task.FromResult(Result<RetrievalResponse>.Failure(
                new Error("storage.query_vector_missing", "Hybrid query requires QueryVector.", ErrorType.Validation)));
        }

        return ExecuteQueryAsync(_dialect.BuildHybridQuerySql(), requestContext, query, cancellationToken);
    }

    public async Task<Result<RetrievalResponse>> ExpandByLogicalSectionAsync(
        IRequestContext requestContext,
        IReadOnlyCollection<StructuralExpansionSeed> seeds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (seeds.Count == 0 || limit <= 0)
        {
            return Result<RetrievalResponse>.Success(new RetrievalResponse([]));
        }

        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            await using var connection = await _dataSources.Read.OpenConnectionAsync(cancellationToken);

            var nodes = new List<RetrievedNode>(limit);
            var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var seed in seeds)
            {
                if (nodes.Count >= limit)
                {
                    break;
                }

                await using var command = new NpgsqlCommand(SectionExpansionSql, connection);
                SqlParameterBinder.BindContext(command, requestContext);
                command.Parameters.AddWithValue("document_id", seed.DocumentId);
                command.Parameters.AddWithValue("section_prefix", seed.SectionPathPrefix + "%");
                command.Parameters.AddWithValue("section_limit", limit - nodes.Count);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var nodeId = reader.GetString(0);
                    if (!seenNodeIds.Add(nodeId))
                    {
                        continue;
                    }

                    nodes.Add(MapNode(reader));

                    if (nodes.Count >= limit)
                    {
                        break;
                    }
                }
            }

            return Result<RetrievalResponse>.Success(new RetrievalResponse(nodes));
        }
        catch (Exception ex)
        {
            return Result<RetrievalResponse>.Failure(
                new Error("storage.query_failed", ex.Message, ErrorType.Unexpected));
        }
    }

    public async Task<Result<RetrievalResponse>> ExpandAdjacentChunksAsync(
        IRequestContext requestContext,
        IReadOnlyCollection<AdjacentExpansionSeed> seeds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (seeds.Count == 0 || limit <= 0)
        {
            return Result<RetrievalResponse>.Success(new RetrievalResponse([]));
        }

        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            await using var connection = await _dataSources.Read.OpenConnectionAsync(cancellationToken);

            var nodes = new List<RetrievedNode>(limit);
            var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var seed in seeds)
            {
                if (nodes.Count >= limit)
                {
                    break;
                }

                await using var command = new NpgsqlCommand(AdjacentExpansionSql, connection);
                SqlParameterBinder.BindContext(command, requestContext);
                command.Parameters.AddWithValue("document_id", seed.DocumentId);
                command.Parameters.AddWithValue("anchor_id", seed.AnchorNodeId);
                command.Parameters.AddWithValue("adjacent_limit", limit - nodes.Count);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var nodeId = reader.GetString(0);
                    if (!seenNodeIds.Add(nodeId))
                    {
                        continue;
                    }

                    nodes.Add(MapNode(reader));

                    if (nodes.Count >= limit)
                    {
                        break;
                    }
                }
            }

            return Result<RetrievalResponse>.Success(new RetrievalResponse(nodes));
        }
        catch (Exception ex)
        {
            return Result<RetrievalResponse>.Failure(
                new Error("storage.query_failed", ex.Message, ErrorType.Unexpected));
        }
    }

    public async Task<Result<IReadOnlyCollection<RetrievedNode>>> GetNodesByIdsAsync(
        IRequestContext requestContext,
        IReadOnlyCollection<string> nodeIds,
        CancellationToken cancellationToken = default)
    {
        if (nodeIds.Count == 0)
        {
            return Result<IReadOnlyCollection<RetrievedNode>>.Success([]);
        }

        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            await using var connection = await _dataSources.Read.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(SelectByIdsSql, connection);
            SqlParameterBinder.BindContext(command, requestContext);
            command.Parameters.AddWithValue("node_ids", NpgsqlDbType.Array | NpgsqlDbType.Text, nodeIds.ToArray());

            var nodes = new List<RetrievedNode>(nodeIds.Count);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                nodes.Add(MapNode(reader));
            }

            return Result<IReadOnlyCollection<RetrievedNode>>.Success(nodes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyCollection<RetrievedNode>>.Failure(
                new Error("storage.query_failed", ex.Message, ErrorType.Unexpected));
        }
    }

    public async Task<Result<IReadOnlyCollection<StructuralDiffResult>>> GetStructuralDiffsAsync(
        IRequestContext requestContext,
        IReadOnlyCollection<StructuralDiffRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return Result<IReadOnlyCollection<StructuralDiffResult>>.Success([]);
        }

        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            await using var connection = await _dataSources.Read.OpenConnectionAsync(cancellationToken);

            var diffs = new List<StructuralDiffResult>(requests.Count);
            foreach (var request in requests)
            {
                var left = await SelectDiffNodeAsync(connection, requestContext, request.DocumentGroupId, request.LeftVersion, request.LogicalPath, cancellationToken);
                var right = await SelectDiffNodeAsync(connection, requestContext, request.DocumentGroupId, request.RightVersion, request.LogicalPath, cancellationToken);

                if (left is null || right is null)
                {
                    continue;
                }

                diffs.Add(new StructuralDiffResult(
                    request.DocumentGroupId,
                    request.LeftVersion,
                    request.RightVersion,
                    request.LogicalPath,
                    left.Text,
                    right.Text,
                    BuildUnifiedDiff(left.Text, right.Text)));
            }

            return Result<IReadOnlyCollection<StructuralDiffResult>>.Success(diffs);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyCollection<StructuralDiffResult>>.Failure(
                new Error("storage.query_failed", ex.Message, ErrorType.Unexpected));
        }
    }

    private async Task<Result<RetrievalResponse>> ExecuteQueryAsync(
        string sql,
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            StorageGuard.EnsureScopedContext(requestContext);
            await using var connection = await _dataSources.Read.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(sql, connection);
            SqlParameterBinder.BindContext(command, requestContext);
            SqlParameterBinder.BindRetrievalQuery(command, query);

            var nodes = new List<RetrievedNode>(query.TopK);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                nodes.Add(MapNode(reader));
            }

            return Result<RetrievalResponse>.Success(new RetrievalResponse(nodes));
        }
        catch (Exception ex)
        {
            return Result<RetrievalResponse>.Failure(
                new Error("storage.query_failed", ex.Message, ErrorType.Unexpected));
        }
    }

    private static RetrievedNode MapNode(NpgsqlDataReader reader)
    {
        var nodeType = reader.IsDBNull(2) ? "paragraph" : reader.GetString(2);
        var rawText = reader.GetString(3);
        var text = string.Equals(nodeType, "table", StringComparison.OrdinalIgnoreCase)
            ? TableProjectionFormatter.FormatForPrompt(rawText)
            : rawText;

        var fidelity = reader.IsDBNull(5) ? "standard" : reader.GetString(5);
        int? page = reader.IsDBNull(6) ? null : reader.GetInt32(6);

        BoundingBoxDto? boundingBox = null;
        if (string.Equals(fidelity, "high", StringComparison.OrdinalIgnoreCase) &&
            !reader.IsDBNull(6) &&
            !reader.IsDBNull(7) &&
            !reader.IsDBNull(8) &&
            !reader.IsDBNull(9) &&
            !reader.IsDBNull(10))
        {
            boundingBox = new BoundingBoxDto(
                page!.Value,
                Convert.ToSingle(reader.GetDouble(7)),
                Convert.ToSingle(reader.GetDouble(8)),
                Convert.ToSingle(reader.GetDouble(9)),
                Convert.ToSingle(reader.GetDouble(10)));
        }

        return new RetrievedNode(
            NodeId: reader.GetString(0),
            DocumentId: reader.GetString(1),
            NodeType: nodeType,
            Text: text,
            Score: reader.GetDouble(4),
            FidelityLevel: fidelity,
            PageNumber: page,
            BoundingBox: boundingBox,
            LogicalPath: reader.IsDBNull(11) ? null : reader.GetString(11),
            Provenance: new RetrievalProvenance(
                PageNumber: page,
                BoundingBox: boundingBox,
                LogicalPath: reader.IsDBNull(11) ? null : reader.GetString(11)),
            DocumentGroupId: reader.IsDBNull(12) ? null : reader.GetString(12),
            VersionNumber: reader.IsDBNull(13) ? null : reader.GetString(13),
            ReferencedNodeIds: reader.IsDBNull(14) ? [] : reader.GetFieldValue<string[]>(14));
    }

    private static async Task<RetrievedNode?> SelectDiffNodeAsync(
        NpgsqlConnection connection,
        IRequestContext requestContext,
        string documentGroupId,
        string versionNumber,
        string logicalPath,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(SelectDiffNodeSql, connection);
        SqlParameterBinder.BindContext(command, requestContext);
        command.Parameters.AddWithValue("document_group_id", documentGroupId);
        command.Parameters.AddWithValue("version_number", versionNumber);
        command.Parameters.AddWithValue("logical_path", logicalPath);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapNode(reader);
    }

    private static string BuildUnifiedDiff(string leftText, string rightText)
    {
        var leftLines = (leftText ?? string.Empty).Split('\n');
        var rightLines = (rightText ?? string.Empty).Split('\n');
        var max = Math.Max(leftLines.Length, rightLines.Length);
        var lines = new List<string>(max + 2)
        {
            "--- left",
            "+++ right"
        };

        for (var i = 0; i < max; i++)
        {
            var left = i < leftLines.Length ? leftLines[i] : string.Empty;
            var right = i < rightLines.Length ? rightLines[i] : string.Empty;

            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                lines.Add(" " + left);
            }
            else
            {
                if (!string.IsNullOrEmpty(left))
                {
                    lines.Add("-" + left);
                }

                if (!string.IsNullOrEmpty(right))
                {
                    lines.Add("+" + right);
                }
            }
        }

        return string.Join('\n', lines);
    }
}
