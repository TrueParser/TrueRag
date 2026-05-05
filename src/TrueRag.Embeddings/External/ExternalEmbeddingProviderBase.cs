using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;
using TrueRag.Core.Validation;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.Embeddings.External;

internal abstract class ExternalEmbeddingProviderBase : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    protected ExternalEmbeddingProviderBase(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public abstract string Name { get; }

    public abstract EmbeddingProviderCapabilities Capabilities { get; }

    public abstract Task<EmbedTextResult> EmbedTextAsync(EmbedTextRequest request, CancellationToken cancellationToken = default);

    public abstract Task<EmbedBatchResult> EmbedBatchAsync(EmbedBatchRequest request, CancellationToken cancellationToken = default);

    protected async Task<HttpResponseMessage> SendWithResilienceAsync(
        HttpRequestMessage request,
        ExternalEmbeddingResilienceOptions resilience,
        CancellationToken cancellationToken)
    {
        var attempts = resilience.MaxRetries + 1;
        var rng = Random.Shared;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(resilience.TimeoutSeconds));

            try
            {
                var clone = await CloneRequestAsync(request);
                var response = await _httpClient.SendAsync(clone, linkedCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                lastError = new HttpRequestException($"External embedding provider returned status {(int)response.StatusCode}.");
                _logger.LogWarning("External embedding provider call failed with status {StatusCode} on attempt {Attempt}/{Attempts}.", (int)response.StatusCode, attempt, attempts);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                _logger.LogWarning("External embedding provider call failed on attempt {Attempt}/{Attempts}. ErrorType={ErrorType}", attempt, attempts, ex.GetType().Name);
            }

            if (attempt < attempts)
            {
                var delay = resilience.BaseDelayMilliseconds * attempt + rng.Next(0, resilience.MaxJitterMilliseconds + 1);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("External embedding provider request failed after retries.", lastError);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var contentString = await request.Content.ReadAsStringAsync();
            clone.Content = new StringContent(contentString, System.Text.Encoding.UTF8, request.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        return clone;
    }
}
