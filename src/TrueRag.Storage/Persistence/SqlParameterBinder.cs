using Npgsql;
using NpgsqlTypes;
using TrueRag.Core.Context;
using TrueRag.Core.Models;

namespace TrueRag.Storage.Persistence;

internal static class SqlParameterBinder
{
    public static void BindContext(NpgsqlCommand command, IRequestContext requestContext)
    {
        command.Parameters.AddWithValue("tenant_id", requestContext.TenantId);
        command.Parameters.AddWithValue("app_id", requestContext.AppId);
        command.Parameters.AddWithValue("collection_id", requestContext.CollectionId);

        if (requestContext.AllowedDocumentGroups.Count == 0)
        {
            command.Parameters.AddWithValue("acl_groups", NpgsqlDbType.Array | NpgsqlDbType.Text, Array.Empty<string>());
        }
        else
        {
            command.Parameters.AddWithValue("acl_groups", NpgsqlDbType.Array | NpgsqlDbType.Text, requestContext.AllowedDocumentGroups.ToArray());
        }

        var emptyFidelityParameter = command.Parameters.Add("required_fidelity", NpgsqlDbType.Text);
        emptyFidelityParameter.Value = DBNull.Value;
    }

    public static void BindRetrievalQuery(NpgsqlCommand command, RetrievalQuery query)
    {
        command.Parameters.AddWithValue("query_text", query.QueryText);
        command.Parameters.AddWithValue("top_k", query.TopK);

        if (query.QueryVector is not null)
        {
            command.Parameters.AddWithValue("query_vector", NpgsqlDbType.Array | NpgsqlDbType.Real, query.QueryVector);
        }

        if (query.Filters is not null &&
            query.Filters.TryGetValue("fidelity_level", out var fidelityLevel) &&
            !string.IsNullOrWhiteSpace(fidelityLevel))
        {
            command.Parameters["required_fidelity"].Value = fidelityLevel;
        }
    }
}
