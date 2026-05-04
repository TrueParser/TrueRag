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
    public async Task SearchTextAsync_EnforcesTenantAppCollectionAndAclPredicates_WithDefaultDeny()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await TruncateNodesAsync();
        using var provider = BuildProvider();

        var ingestion = provider.GetRequiredService<IIngestionRepository>();
        var retrieval = provider.GetRequiredService<IRetrievalService>();

        var tenantAppCollectionContext = new RequestContext("tenant-a", "app-core", "user-1", ["reader"], ["eng"], "collection-main");
        var otherGroupContext = new RequestContext("tenant-a", "app-core", "user-2", ["reader"], ["hr"], "collection-main");
        var emptyAclContext = new RequestContext("tenant-a", "app-core", "user-3", ["reader"], [], "collection-main");
        var otherCollectionContext = new RequestContext("tenant-a", "app-core", "user-4", ["reader"], ["eng"], "collection-alt");

        var engPayload = CreatePayload("doc-eng", "Budget forecast for engineering", "eng", "collection-main");
        var hrPayload = CreatePayload("doc-hr", "Budget forecast for human resources", "hr");
        var otherTenantPayload = CreatePayload("doc-other-tenant", "Budget forecast for tenant b", "eng");
        var otherAppPayload = CreatePayload("doc-other-app", "Budget forecast for app other", "eng");
        var otherCollectionPayload = CreatePayload("doc-other-collection", "Budget forecast for alternate collection", "eng", "collection-alt");

        Assert.True((await ingestion.UpsertDocumentAsync(tenantAppCollectionContext, engPayload)).IsSuccess);
        Assert.True((await ingestion.UpsertDocumentAsync(otherGroupContext, hrPayload)).IsSuccess);
        Assert.True((await ingestion.UpsertDocumentAsync(new RequestContext("tenant-b", "app-core", "user-5", ["reader"], ["eng"], "collection-main"), otherTenantPayload)).IsSuccess);
        Assert.True((await ingestion.UpsertDocumentAsync(new RequestContext("tenant-a", "app-other", "user-6", ["reader"], ["eng"], "collection-main"), otherAppPayload)).IsSuccess);
        Assert.True((await ingestion.UpsertDocumentAsync(otherCollectionContext, otherCollectionPayload)).IsSuccess);

        var query = new RetrievalQuery("budget forecast", QueryVector: null, TopK: 10);

        var allowedResult = await retrieval.SearchTextAsync(tenantAppCollectionContext, query);
        Assert.True(allowedResult.IsSuccess);
        Assert.Contains(allowedResult.Value!.Nodes, node => node.DocumentId == "doc-eng");
        Assert.DoesNotContain(allowedResult.Value.Nodes, node => node.DocumentId == "doc-hr");
        Assert.DoesNotContain(allowedResult.Value.Nodes, node => node.DocumentId == "doc-other-tenant");
        Assert.DoesNotContain(allowedResult.Value.Nodes, node => node.DocumentId == "doc-other-app");
        Assert.DoesNotContain(allowedResult.Value.Nodes, node => node.DocumentId == "doc-other-collection");

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
        var context = new RequestContext("tenant-sync", "app-sync", "user-sync", ["writer"], ["finance"], "collection-sync");
        var payload = CreatePayload("doc-sync", "Quarterly results table", "finance", "collection-sync");

        var upsert = await ingestion.UpsertDocumentAsync(context, payload);

        Assert.True(upsert.IsSuccess);

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM nodes WHERE tenant_id = @tenant AND app_id = @app AND collection_id = @collection AND document_id = @doc",
            connection);
        command.Parameters.AddWithValue("tenant", "tenant-sync");
        command.Parameters.AddWithValue("app", "app-sync");
        command.Parameters.AddWithValue("collection", "collection-sync");
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

    private static IngestionRequestDto CreatePayload(string documentId, string text, string group, string collectionId = "collection-main")
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
            ],
            CollectionId: collectionId);
    }
}
