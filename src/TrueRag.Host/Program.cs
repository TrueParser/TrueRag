using TrueRag.Api;
using TrueRag.Api.Extensions;
using TrueRag.Conversations;
using TrueRag.Embeddings;
using TrueRag.Ingestion;
using TrueRag.Retrieval;
using TrueRag.Retrieval.Configuration;
using TrueRag.Host.Migrations;
using TrueRag.Storage;
using TrueRag.Storage.Persistence;
using TrueRag.Workers;

var builder = WebApplication.CreateBuilder(args);

static DatabaseEngine ParseEngine(string? configuredValue, string key)
{
    if (string.IsNullOrWhiteSpace(configuredValue))
    {
        throw new InvalidOperationException($"{key} must be configured to either 'CrateDb' or 'PostgreSql'.");
    }

    if (Enum.TryParse<DatabaseEngine>(configuredValue, ignoreCase: true, out var parsed))
    {
        return parsed;
    }

    throw new InvalidOperationException($"{key} value '{configuredValue}' is invalid. Allowed values: CrateDb, PostgreSql.");
}

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
builder.Services.AddOptions<SchemaMigrationStartupOptions>()
    .BindConfiguration(SchemaMigrationStartupOptions.SectionName);

var writeEngine = ParseEngine(builder.Configuration["Storage:WriteEngine"], "Storage:WriteEngine");
var readEngine = ParseEngine(builder.Configuration["Storage:ReadEngine"], "Storage:ReadEngine");

builder.Services.AddTrueRagStorage(
    writeConnectionString: builder.Configuration.GetConnectionString("DbWrite") ?? string.Empty,
    readConnectionString: builder.Configuration.GetConnectionString("DbRead") ?? string.Empty,
    writeEngine: writeEngine,
    readEngine: readEngine);

builder.Services.PostConfigure<RetrievalEngineOptions>(options =>
{
    if (!string.Equals(options.HybridFusionMode, "Auto", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    options.HybridFusionMode = readEngine == DatabaseEngine.PostgreSql ? "SplitRrf" : "Sql";
});

builder.Services.AddTrueRagWorkers();

var app = builder.Build();

if (await MigrationCommandHandler.TryHandleAsync(args, app.Services, app.Logger, app.Lifetime.ApplicationStopping))
{
    return;
}

await SchemaMigrationStartupPolicy.EnforceAsync(app.Services, app.Logger, app.Lifetime.ApplicationStopping);

app.UseTrueRagApiPipeline();

app.MapGet("/", () => "TrueRAG Host");
app.MapControllers();

app.Run();

public partial class Program;
