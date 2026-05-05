using Microsoft.Extensions.Options;
using TrueRag.Embeddings;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OnnxProfileSelectionOptionsValidatorTests
{
    [Fact]
    public void Validate_Fails_WhenDefaultProfileUnsupported()
    {
        var validator = new OnnxProfileSelectionOptionsValidator(new OnnxModelProfileRegistry());
        var options = new OnnxProfileSelectionOptions { DefaultProfileName = "unknown-profile" };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenExpectedDimensionsMismatch()
    {
        var validator = new OnnxProfileSelectionOptionsValidator(new OnnxModelProfileRegistry());
        var options = new OnnxProfileSelectionOptions
        {
            DefaultProfileName = "bge-base-en-v1.5",
            ExpectedVectorDimensions = 384
        };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenExpectedMaxTokensMismatch()
    {
        var validator = new OnnxProfileSelectionOptionsValidator(new OnnxModelProfileRegistry());
        var options = new OnnxProfileSelectionOptions
        {
            DefaultProfileName = "bge-small-en-v1.5",
            ExpectedVectorMaxTokens = 256
        };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_Succeeds_ForSupportedScopedOverrides()
    {
        var validator = new OnnxProfileSelectionOptionsValidator(new OnnxModelProfileRegistry());
        var options = new OnnxProfileSelectionOptions
        {
            DefaultProfileName = "bge-small-en-v1.5",
            ExpectedVectorDimensions = 384,
            ExpectedVectorMaxTokens = 512,
            ScopedProfiles =
            [
                new EmbeddingScopeProfileSelection
                {
                    TenantId = "tenant-1",
                    AppId = "app-1",
                    CollectionId = "collection-1",
                    ProfileName = "multi-qa-minilm-l6-cos-v1"
                }
            ]
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}
