using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrueRag.Core.Models;
using TrueRag.Embeddings;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OpenAiEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedTextAsync_MapsOpenAiResponseToVector()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"embedding\":[0.1,0.2,0.3]}]}")
            });

        var provider = CreateProvider(new HttpClient(handler));
        var descriptor = new EmbeddingModelDescriptor("openai", "text-embedding-3-small", 1536, 8192, EmbeddingDistanceMetric.Cosine);

        var result = await provider.EmbedTextAsync(new EmbedTextRequest(new EmbeddingInput("hello"), descriptor));

        Assert.Equal(3, result.Vector.Length);
    }

    [Fact]
    public async Task EmbedTextAsync_RetriesOnFailure()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"embedding\":[0.1]}]}") });

        var provider = CreateProvider(new HttpClient(handler));
        var descriptor = new EmbeddingModelDescriptor("openai", "text-embedding-3-small", 1536, 8192, EmbeddingDistanceMetric.Cosine);

        var result = await provider.EmbedTextAsync(new EmbedTextRequest(new EmbeddingInput("hello"), descriptor));

        Assert.Single(result.Vector);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task EmbedBatchAsync_UsesOpenAiIndexOrdering()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"index\":1,\"embedding\":[9]},{\"index\":0,\"embedding\":[1]}]}")
            });

        var provider = CreateProvider(new HttpClient(handler));
        var descriptor = new EmbeddingModelDescriptor("openai", "text-embedding-3-small", 1536, 8192, EmbeddingDistanceMetric.Cosine);

        var result = await provider.EmbedBatchAsync(new EmbedBatchRequest(
            [new EmbeddingInput("a"), new EmbeddingInput("b")],
            descriptor));

        var vectors = result.Vectors.ToArray();
        Assert.Equal(1f, vectors[0][0]);
        Assert.Equal(9f, vectors[1][0]);
    }

    private static OpenAiEmbeddingProvider CreateProvider(HttpClient client)
    {
        var options = Options.Create(new OpenAiEmbeddingOptions
        {
            Enabled = true,
            ProviderName = "openai",
            ApiKey = "secret",
            Endpoint = "https://api.openai.com/v1/embeddings",
            Model = "text-embedding-3-small",
            Dimensions = 1536,
            MaxTokens = 8192,
            Resilience = new ExternalEmbeddingResilienceOptions
            {
                TimeoutSeconds = 5,
                MaxRetries = 2,
                BaseDelayMilliseconds = 1,
                MaxJitterMilliseconds = 0
            }
        });

        return new OpenAiEmbeddingProvider(client, options, NullLogger<OpenAiEmbeddingProvider>.Instance);
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
