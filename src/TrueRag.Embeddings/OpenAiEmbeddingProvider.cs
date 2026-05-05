using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Core.Models;
using TrueRag.Core.Validation;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.External;

namespace TrueRag.Embeddings;

internal sealed class OpenAiEmbeddingProvider : ExternalEmbeddingProviderBase
{
    private readonly OpenAiEmbeddingOptions _options;
    private readonly ILogger<OpenAiEmbeddingProvider> _logger;

    public OpenAiEmbeddingProvider(HttpClient httpClient, IOptions<OpenAiEmbeddingOptions> options, ILogger<OpenAiEmbeddingProvider> logger)
        : base(httpClient, logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public override string Name => _options.ProviderName;

    public override EmbeddingProviderCapabilities Capabilities { get; } = new(
        EmbeddingCapabilityFlags.SingleText | EmbeddingCapabilityFlags.BatchText | EmbeddingCapabilityFlags.ExternalExecution,
        128,
        [EmbeddingDistanceMetric.Cosine]);

    public override async Task<EmbedTextResult> EmbedTextAsync(EmbedTextRequest request, CancellationToken cancellationToken = default)
    {
        EmbeddingContractValidator.ValidateRequest(request);

        var payload = new
        {
            model = _options.Model,
            input = request.Input.Text
        };

        var response = await SendAsync(payload, cancellationToken);
        var vector = ParseSingleVector(response);

        return new EmbedTextResult(
            vector,
            new EmbeddingModelDescriptor(_options.ProviderName, _options.Model, _options.Dimensions, _options.MaxTokens, EmbeddingDistanceMetric.Cosine),
            new EmbeddingUsage(0));
    }

    public override async Task<EmbedBatchResult> EmbedBatchAsync(EmbedBatchRequest request, CancellationToken cancellationToken = default)
    {
        EmbeddingContractValidator.ValidateRequest(request);

        var payload = new
        {
            model = _options.Model,
            input = request.Inputs.Select(item => item.Text).ToArray()
        };

        var response = await SendAsync(payload, cancellationToken);
        var vectors = ParseVectors(response);

        return new EmbedBatchResult(
            vectors,
            new EmbeddingModelDescriptor(_options.ProviderName, _options.Model, _options.Dimensions, _options.MaxTokens, EmbeddingDistanceMetric.Cosine),
            new EmbeddingUsage(0));
    }

    private async Task<JsonDocument> SendAsync(object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await SendWithResilienceAsync(request, _options.Resilience, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI embedding response. ResponseSize={ResponseSize}", json.Length);
            throw new InvalidOperationException("Failed to parse OpenAI embedding response.", ex);
        }
    }

    private static float[] ParseSingleVector(JsonDocument json)
    {
        var array = ParseVectors(json);
        return array.FirstOrDefault() ?? throw new InvalidOperationException("OpenAI embedding response did not include vectors.");
    }

    private static IReadOnlyCollection<float[]> ParseVectors(JsonDocument json)
    {
        if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("OpenAI embedding response missing 'data' array.");
        }

        var indexed = new List<(int index, float[] vector)>();
        var ordinal = 0;
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("embedding", out var embedding) || embedding.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var vector = embedding.EnumerateArray().Select(static value => value.GetSingle()).ToArray();
            var index = item.TryGetProperty("index", out var indexElement) && indexElement.ValueKind == JsonValueKind.Number
                ? indexElement.GetInt32()
                : ordinal;
            indexed.Add((index, vector));
            ordinal++;
        }

        if (indexed.Count == 0)
        {
            throw new InvalidOperationException("OpenAI embedding response did not include usable vectors.");
        }

        return indexed
            .OrderBy(static item => item.index)
            .Select(static item => item.vector)
            .ToArray();
    }
}
