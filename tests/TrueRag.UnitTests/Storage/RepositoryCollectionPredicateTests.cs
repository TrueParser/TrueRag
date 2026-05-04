using System.Reflection;
using TrueRag.Storage;

namespace TrueRag.UnitTests.Storage;

public sealed class RepositoryCollectionPredicateTests
{
    [Fact]
    public void RetrievalRepository_InternalSql_WithAclOrDocFilters_AlwaysIncludesCollectionPredicate()
    {
        var fields = typeof(RetrievalRepository)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .ToArray();

        Assert.NotEmpty(fields);

        foreach (var field in fields)
        {
            var sql = field.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(sql))
            {
                continue;
            }

            var hasAclOrDocFilter = sql.Contains("allowed_document_groups", StringComparison.OrdinalIgnoreCase)
                                  || sql.Contains("document_group_id", StringComparison.OrdinalIgnoreCase)
                                  || sql.Contains("logical_path", StringComparison.OrdinalIgnoreCase)
                                  || sql.Contains("id = ANY(@node_ids)", StringComparison.OrdinalIgnoreCase);

            if (!hasAclOrDocFilter)
            {
                continue;
            }

            Assert.Contains("collection_id = @collection_id", sql, StringComparison.OrdinalIgnoreCase);
        }
    }
}
