using TrueRag.Api;
using TrueRag.Api.Context;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Ingestion;
using TrueRag.Ingestion.Execution;
using TrueRag.Retrieval;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;
using TrueRag.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTrueRagApi();
builder.Services.AddTrueRagIngestion();
builder.Services.AddTrueRagRetrieval();
builder.Services.AddTrueRagStorage(
    writeConnectionString: builder.Configuration.GetConnectionString("DbWrite") ?? string.Empty,
    readConnectionString: builder.Configuration.GetConnectionString("DbRead") ?? string.Empty,
    writeEngine: DatabaseEngine.CrateDb,
    readEngine: DatabaseEngine.CrateDb);
builder.Services.AddTrueRagWorkers();

var app = builder.Build();

app.UseTrueRagRequestContext();

app.MapGet("/", () => "TrueRAG Host");
app.MapGet("/api/v1/context", (IRequestContext context) => Results.Ok(new
{
    context.TenantId,
    context.AppId,
    context.UserId,
    Roles = context.Roles,
    AllowedDocumentGroups = context.AllowedDocumentGroups
}));

app.MapPost(
    "/api/v1/ingest/async",
    async (IRequestContext context, IIngestionExecutionService executionService, IngestionRequestDto payload, CancellationToken cancellationToken) =>
    {
        var result = await executionService.IngestAsyncBuffered(context, payload, cancellationToken);
        if (result.IsFailure)
        {
            return Results.BadRequest(result.Error);
        }

        return Results.Accepted(value: new
        {
            result.Value!.NodeId,
            result.Value.TenantId,
            result.Value.AppId,
            result.Value.WalPath,
            result.Value.WalSegmentId,
            result.Value.WalOffset,
            result.Value.WalLength
        });
    });

app.MapPost(
    "/api/v1/ingest/sync",
    async (IRequestContext context, IIngestionExecutionService executionService, IngestionRequestDto payload, CancellationToken cancellationToken) =>
    {
        var result = await executionService.IngestSyncAsync(context, payload, cancellationToken);
        return result.IsSuccess
            ? Results.Ok()
            : Results.BadRequest(result.Error);
    });

app.MapPost(
    "/api/v1/search/vector",
    async (IRequestContext context, IRetrievalService retrievalService, RetrievalQuery query, CancellationToken cancellationToken) =>
    {
        var result = await retrievalService.SearchVectorAsync(context, query, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    });

app.MapPost(
    "/api/v1/search/text",
    async (IRequestContext context, IRetrievalService retrievalService, RetrievalQuery query, CancellationToken cancellationToken) =>
    {
        var result = await retrievalService.SearchTextAsync(context, query, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    });

app.MapPost(
    "/api/v1/search/hybrid",
    async (IRequestContext context, IRetrievalService retrievalService, RetrievalQuery query, CancellationToken cancellationToken) =>
    {
        var result = await retrievalService.SearchHybridAsync(context, query, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    });

app.Run();
