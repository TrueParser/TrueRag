using Microsoft.Extensions.Options;
using TrueRag.Embeddings;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OpenAiEmbeddingOptionsValidatorTests
{
    [Fact]
    public void Validate_Succeeds_WhenDisabled()
    {
        var validator = new OpenAiEmbeddingOptionsValidator();
        var options = new OpenAiEmbeddingOptions { Enabled = false };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenEnabledWithoutApiKey()
    {
        var validator = new OpenAiEmbeddingOptionsValidator();
        var options = new OpenAiEmbeddingOptions
        {
            Enabled = true,
            ApiKey = string.Empty
        };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }
}
