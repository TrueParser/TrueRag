using Microsoft.Extensions.Options;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.Embeddings;

internal sealed class EmbeddingModeSelectionOptionsValidator : IValidateOptions<EmbeddingModeSelectionOptions>
{
    public ValidateOptionsResult Validate(string? name, EmbeddingModeSelectionOptions options)
    {
        if (!IsModeValue(options.DefaultMode))
        {
            return ValidateOptionsResult.Fail("Embeddings:ModeSelection:DefaultMode must be 'internal_embedding' or 'external_embedding'.");
        }

        foreach (var scoped in options.ScopedModes)
        {
            if (string.IsNullOrWhiteSpace(scoped.TenantId)
                || string.IsNullOrWhiteSpace(scoped.AppId)
                || string.IsNullOrWhiteSpace(scoped.CollectionId)
                || !IsModeValue(scoped.Mode))
            {
                return ValidateOptionsResult.Fail("Embeddings:ModeSelection:ScopedModes entries must define tenant/app/collection and valid mode.");
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsModeValue(string mode)
        => string.Equals(mode, "internal_embedding", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, "external_embedding", StringComparison.OrdinalIgnoreCase);
}
