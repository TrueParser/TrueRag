using Microsoft.Extensions.Options;
using TrueRag.Conversations.Configuration;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Primitives;

namespace TrueRag.Conversations.Llm;

internal sealed class LlmProviderFactory : ILlmProviderFactory
{
    private readonly IReadOnlyDictionary<string, ILlmProvider> _providers;
    private readonly LlmProviderOptions _options;

    public LlmProviderFactory(
        IEnumerable<ILlmProvider> providers,
        IOptions<LlmProviderOptions> options)
    {
        _providers = providers.ToDictionary(
            static provider => provider.ProviderId,
            StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
    }

    public Result<ILlmProvider> Resolve(string? providerId)
    {
        var key = string.IsNullOrWhiteSpace(providerId) ? _options.DefaultProvider : providerId.Trim();
        if (_providers.TryGetValue(key, out var provider))
        {
            return Result<ILlmProvider>.Success(provider);
        }

        return Result<ILlmProvider>.Failure(
            new Error("conversation.provider_not_registered", $"Provider '{key}' is not registered.", ErrorType.Validation));
    }
}
