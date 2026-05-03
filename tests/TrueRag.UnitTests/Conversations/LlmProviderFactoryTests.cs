using Microsoft.Extensions.Options;
using TrueRag.Conversations.Configuration;
using TrueRag.Conversations.Llm;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;

namespace TrueRag.UnitTests.Conversations;

public sealed class LlmProviderFactoryTests
{
    [Fact]
    public void Resolve_ReturnsConfiguredDefaultProvider()
    {
        var parser = new LlmResponseParser();
        var providers = new ILlmProvider[]
        {
            new LocalLlmProvider(parser),
            new OpenAiLlmProvider(parser),
            new AnthropicLlmProvider(parser)
        };
        var factory = new LlmProviderFactory(
            providers,
            Options.Create(new LlmProviderOptions { DefaultProvider = "anthropic" }));

        var resolved = factory.Resolve(null);

        Assert.True(resolved.IsSuccess);
        Assert.Equal("anthropic", resolved.Value!.ProviderId);
    }

    [Fact]
    public async Task Provider_StreamAsync_EmitsChunks_AndFinalChunk()
    {
        var provider = new OpenAiLlmProvider(new LlmResponseParser());
        var chunks = new List<LlmCompletionChunk>();

        await foreach (var chunk in provider.StreamAsync(
                           new LlmCompletionRequest([new LlmMessage("user", "hello")], Stream: true)))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
        Assert.True(chunks.Last().IsFinal);
    }
}
