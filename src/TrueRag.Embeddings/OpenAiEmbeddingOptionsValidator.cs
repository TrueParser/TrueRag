using Microsoft.Extensions.Options;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.Embeddings;

internal sealed class OpenAiEmbeddingOptionsValidator : IValidateOptions<OpenAiEmbeddingOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAiEmbeddingOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ProviderName)
            || string.IsNullOrWhiteSpace(options.ApiKey)
            || string.IsNullOrWhiteSpace(options.Endpoint)
            || string.IsNullOrWhiteSpace(options.Model))
        {
            return ValidateOptionsResult.Fail("Embeddings:OpenAI requires ProviderName, ApiKey, Endpoint, and Model when enabled.");
        }

        if (options.Dimensions <= 0 || options.MaxTokens <= 0)
        {
            return ValidateOptionsResult.Fail("Embeddings:OpenAI Dimensions and MaxTokens must be greater than zero.");
        }

        if (options.Resilience.TimeoutSeconds <= 0
            || options.Resilience.MaxRetries < 0
            || options.Resilience.BaseDelayMilliseconds <= 0
            || options.Resilience.MaxJitterMilliseconds < 0)
        {
            return ValidateOptionsResult.Fail("Embeddings:OpenAI.Resilience values are invalid.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("Embeddings:OpenAI.Endpoint must be an absolute URI.");
        }

        return ValidateOptionsResult.Success;
    }
}
