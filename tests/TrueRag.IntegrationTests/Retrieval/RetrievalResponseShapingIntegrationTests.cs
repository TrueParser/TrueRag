using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.IntegrationTests.Infrastructure;
using TrueRag.Retrieval;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;

namespace TrueRag.IntegrationTests.Retrieval;

public sealed class RetrievalResponseShapingIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    private readonly PostgreSqlIntegrationFixture _fixture;

    public RetrievalResponseShapingIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchTextAsync_ProjectsProvenanceAndConfidence_FromDatabase()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateAsync();
        await SeedNodesAsync();

        using var provider = BuildProvider();
        var service = provider.GetRequiredService<IRetrievalService>();
        var context = new RequestContext("tenant-a", "app-a", "u1", ["reader"], ["g1"]);

        var response = await service.SearchTextAsync(context, new RetrievalQuery("payment obligations", null, 5));

        Assert.True(response.IsSuccess);
        var node = Assert.Single(response.Value!.Nodes);
        Assert.Equal("doc-group-1", node.DocumentGroupId);
        Assert.Equal("v2", node.VersionNumber);
        Assert.NotNull(node.ReferencedNodeIds);
        Assert.Contains("n-ref-1", node.ReferencedNodeIds!);
        Assert.NotNull(node.Provenance);
        Assert.Equal(4, node.Provenance!.PageNumber);
        Assert.Equal("Document/Section9/Paragraph1", node.Provenance.LogicalPath);
        Assert.NotNull(response.Value.RetrievalConfidence);
        Assert.NotNull(response.Value.OverallConfidence);
    }

    private ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RetrievalEngine:RequireHighFidelity"] = "false",
                ["RetrievalEngine:FallbackToStandardRag"] = "true",
                ["RetrievalEngine:EnableSemanticCache"] = "false",
                ["RetrievalEngine:EnableDistributedRateLimit"] = "false",
                ["RetrievalEngine:EnableMultiHopLinking"] = "true",
                ["RetrievalEngine:EnableStructuralDiffing"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddDistributedMemoryCache();
        services.AddTrueRagStorage(
            writeConnectionString: _fixture.ConnectionString,
            readConnectionString: _fixture.ConnectionString,
            writeEngine: DatabaseEngine.PostgreSql,
            readEngine: DatabaseEngine.PostgreSql);
        services.AddTrueRagRetrieval();
        return services.BuildServiceProvider();
    }

    private async Task SeedNodesAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        var sql = """
            INSERT INTO nodes (
                id, document_id, document_group_id, version_number, tenant_id, app_id, allowed_document_groups,
                fidelity_level, parent_id, logical_path, node_type, text, page, x, y, w, h, referenced_node_ids, vector
            ) VALUES (
                @id, @document_id, @document_group_id, @version_number, @tenant_id, @app_id, @allowed_document_groups,
                @fidelity_level, @parent_id, @logical_path, @node_type, @text, @page, @x, @y, @w, @h, @referenced_node_ids, @vector
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", "n-main-1");
        command.Parameters.AddWithValue("document_id", "doc-1");
        command.Parameters.AddWithValue("document_group_id", "doc-group-1");
        command.Parameters.AddWithValue("version_number", "v2");
        command.Parameters.AddWithValue("tenant_id", "tenant-a");
        command.Parameters.AddWithValue("app_id", "app-a");
        command.Parameters.AddWithValue("allowed_document_groups", new[] { "g1" });
        command.Parameters.AddWithValue("fidelity_level", "high");
        command.Parameters.AddWithValue("parent_id", DBNull.Value);
        command.Parameters.AddWithValue("logical_path", "Document/Section9/Paragraph1");
        command.Parameters.AddWithValue("node_type", "paragraph");
        command.Parameters.AddWithValue("text", "payment obligations are described here");
        command.Parameters.AddWithValue("page", 4);
        command.Parameters.AddWithValue("x", 10d);
        command.Parameters.AddWithValue("y", 12d);
        command.Parameters.AddWithValue("w", 100d);
        command.Parameters.AddWithValue("h", 25d);
        command.Parameters.AddWithValue("referenced_node_ids", new[] { "n-ref-1" });
        command.Parameters.AddWithValue("vector", new[] { 0.1f, 0.2f, 0.3f });
        await command.ExecuteNonQueryAsync();
    }

    private async Task TruncateAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("TRUNCATE TABLE nodes, conversation_messages, conversation_thread_states", connection);
        await command.ExecuteNonQueryAsync();
    }
}
