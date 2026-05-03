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

namespace TrueRag.IntegrationTests.Storage;

public sealed class AclIsolationIntegrationTests : IClassFixture<PostgreSqlIntegrationFixture>
{
    private readonly PostgreSqlIntegrationFixture _fixture;

    public AclIsolationIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchTextAsync_EnforcesTenantAppAndAclPredicates_WithDefaultDeny()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateNodesAsync();
        using var provider = BuildProvider();

        var ingestion = provider.GetRequiredService<IIngestionRepository>();
        var retrieval = provider.GetRequiredService<IRetrievalService>();

        var tenantAppContext = new RequestContext("tenant-a", "app-core", "user-1", ["reader"], ["eng"]);
        var otherGroupContext = new RequestContext("tenant-a", "app-core", "user-2", ["reader"], ["hr"]);
        var emptyAclContext = new RequestContext("tenant-a", "app-core", "user-3", ["reader"], []);

        var engPayload = CreatePayload("doc-eng", "Budget forecast for engineering", "eng");
        var hrPayload = CreatePayload("doc-hr", "Budget forecast for human resources", "hr");
        var otherTenantPayload = CreatePayload("doc-other-tenant", "Budget forecast for tenant b", "eng");
        var otherAppPayload = CreatePayload("doc-other-app", "Budget forecast for app other", "eng");

        Assert.True((await ingestion.UpsertDocumentAsync(tenantAppContext, engPayload)).IsSuccess);
        Assert.True((await ingestion.UpsertDocumentAsync(otherGroupContext, hrPayload)).IsSuccess);
        Assert.True((await ingestion.UpsertDocumentAsync(new RequestContext("tenant-b", "app-core", "user-4", ["reader"], ["eng"]), otherTenantPayload)).IsSuccess);
        Assert.True((await ingestion.UpsertDocumentAsync(new RequestContext("tenant-a", "app-other", "user-5", ["reader"], ["eng"]), otherAppPayload)).IsSuccess);

        var query = new RetrievalQuery("budget forecast", QueryVector: null, TopK: 10);

        var allowedResult = await retrieval.SearchTextAsync(tenantAppContext, query);
        Assert.True(allowedResult.IsSuccess);
        Assert.Contains(allowedResult.Value!.Nodes, node => node.DocumentId == "doc-eng");
        Assert.DoesNotContain(allowedResult.Value.Nodes, node => node.DocumentId == "doc-hr");
        Assert.DoesNotContain(allowedResult.Value.Nodes, node => node.DocumentId == "doc-other-tenant");
        Assert.DoesNotContain(allowedResult.Value.Nodes, node => node.DocumentId == "doc-other-app");

        var deniedResult = await retrieval.SearchTextAsync(emptyAclContext, query);
        Assert.True(deniedResult.IsSuccess);
        Assert.Empty(deniedResult.Value!.Nodes);
    }

    [Fact]
    public async Task UpsertDocumentAsync_PersistsSyncWrite_ToDatabase()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateNodesAsync();
        using var provider = BuildProvider();
        var ingestion = provider.GetRequiredService<IIngestionRepository>();
        var context = new RequestContext("tenant-sync", "app-sync", "user-sync", ["writer"], ["finance"]);
        var payload = CreatePayload("doc-sync", "Quarterly results table", "finance");

        var upsert = await ingestion.UpsertDocumentAsync(context, payload);

        Assert.True(upsert.IsSuccess);

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM nodes WHERE tenant_id = @tenant AND app_id = @app AND document_id = @doc",
            connection);
        command.Parameters.AddWithValue("tenant", "tenant-sync");
        command.Parameters.AddWithValue("app", "app-sync");
        command.Parameters.AddWithValue("doc", "doc-sync");

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1L, count);
    }

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RetrievalEngine:RequireHighFidelity"] = "false",
                ["RetrievalEngine:FallbackToStandardRag"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTrueRagStorage(
            writeConnectionString: _fixture.ConnectionString,
            readConnectionString: _fixture.ConnectionString,
            writeEngine: DatabaseEngine.PostgreSql,
            readEngine: DatabaseEngine.PostgreSql);
        services.AddTrueRagRetrieval();
        return services.BuildServiceProvider();
    }

    private async Task TruncateNodesAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("TRUNCATE TABLE nodes", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static IngestionRequestDto CreatePayload(string documentId, string text, string group)
    {
        return new IngestionRequestDto(
            DocumentId: documentId,
            DocumentGroupId: "group-1",
            VersionNumber: "1.0",
            AllowedDocumentGroups: [group],
            Fidelity: "standard",
            Chunks:
            [
                new ChunkDto(
                    Id: documentId + "-n1",
                    ParentId: null,
                    LogicalPath: null,
                    Type: "Paragraph",
                    Text: text,
                    BoundingBox: null,
                    ReferencedNodeIds: null,
                    Vector: [0.1f, 0.2f, 0.3f])
            ]);
    }
}
