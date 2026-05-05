using TrueRag.Api;
using TrueRag.Api.Extensions;
using TrueRag.Conversations;
using TrueRag.Embeddings;
using TrueRag.Ingestion;
using TrueRag.Retrieval;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;
using TrueRag.Workers;

var builder = WebApplication.CreateBuilder(args);

var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });
}

builder.Services.AddTrueRagApi();
builder.Services.AddTrueRagIngestion();
builder.Services.AddTrueRagRetrieval();
builder.Services.AddTrueRagConversations();
builder.Services.AddTrueRagEmbeddings();
builder.Services.AddTrueRagStorage(
    writeConnectionString: builder.Configuration.GetConnectionString("DbWrite") ?? string.Empty,
    readConnectionString: builder.Configuration.GetConnectionString("DbRead") ?? string.Empty,
    writeEngine: DatabaseEngine.CrateDb,
    readEngine: DatabaseEngine.CrateDb);
builder.Services.AddTrueRagWorkers();

var app = builder.Build();

app.UseTrueRagApiPipeline();

app.MapGet("/", () => "TrueRAG Host");
app.MapControllers();

app.Run();

public partial class Program;
