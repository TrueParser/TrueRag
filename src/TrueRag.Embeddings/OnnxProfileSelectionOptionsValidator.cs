using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.Embeddings;

internal sealed partial class OnnxProfileSelectionOptionsValidator(IOnnxModelProfileRegistry profileRegistry) : IValidateOptions<OnnxProfileSelectionOptions>
{
    public ValidateOptionsResult Validate(string? name, OnnxProfileSelectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultProfileName))
        {
            return ValidateOptionsResult.Fail("Embeddings:ProfileSelection:DefaultProfileName is required.");
        }

        try
        {
            var defaultProfile = profileRegistry.GetRequiredProfile(options.DefaultProfileName);
            var defaultError = ValidateProfileCompatibility(defaultProfile, options, "Default");
            if (defaultError is not null)
            {
                return ValidateOptionsResult.Fail(defaultError);
            }
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }

        foreach (var scoped in options.ScopedProfiles)
        {
            if (string.IsNullOrWhiteSpace(scoped.TenantId)
                || string.IsNullOrWhiteSpace(scoped.AppId)
                || string.IsNullOrWhiteSpace(scoped.CollectionId)
                || string.IsNullOrWhiteSpace(scoped.ProfileName))
            {
                return ValidateOptionsResult.Fail("Embeddings:ProfileSelection:ScopedProfiles requires tenant_id/app_id/collection_id/profile_name for each entry.");
            }

            try
            {
                var profile = profileRegistry.GetRequiredProfile(scoped.ProfileName);
                var scopedError = ValidateProfileCompatibility(profile, options, "Scoped");
                if (scopedError is not null)
                {
                    return ValidateOptionsResult.Fail(scopedError);
                }
            }
            catch (Exception ex)
            {
                return ValidateOptionsResult.Fail(ex.Message);
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static string? ValidateProfileCompatibility(OnnxModelProfile profile, OnnxProfileSelectionOptions options, string scope)
    {
        if (options.ExpectedVectorDimensions.HasValue && profile.Dimensions != options.ExpectedVectorDimensions.Value)
        {
            return $"{scope} embedding profile '{profile.Name}' dimensions ({profile.Dimensions}) do not match ExpectedVectorDimensions ({options.ExpectedVectorDimensions.Value}).";
        }

        if (options.ExpectedVectorMaxTokens.HasValue && profile.MaxTokens != options.ExpectedVectorMaxTokens.Value)
        {
            return $"{scope} embedding profile '{profile.Name}' max tokens ({profile.MaxTokens}) do not match ExpectedVectorMaxTokens ({options.ExpectedVectorMaxTokens.Value}).";
        }

        if (!options.AllowGlobalPathFallback && string.IsNullOrWhiteSpace(profile.ModelArtifactsPath))
        {
            return $"{scope} embedding profile '{profile.Name}' must define model artifacts path when AllowGlobalPathFallback is false.";
        }

        if (!string.IsNullOrWhiteSpace(profile.ChecksumSha256) && !ChecksumRegex().IsMatch(profile.ChecksumSha256))
        {
            return $"{scope} embedding profile '{profile.Name}' has invalid ChecksumSha256 format; expected 64 hex characters.";
        }

        return null;
    }

    [GeneratedRegex("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChecksumRegex();
}
