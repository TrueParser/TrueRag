using TrueRag.Core.Models;

namespace TrueRag.Conversations.Llm;

internal sealed class AnthropicLlmProvider : BaseStubLlmProvider
{
    public AnthropicLlmProvider(ILlmResponseParser parser)
        : base(parser)
    {
    }

    public override string ProviderId => "anthropic";

    protected override string BuildRawResponse(LlmCompletionRequest request)
    {
        var question = request.Messages.LastOrDefault(static message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;
        return $$"""
               {
                 "answer":"Anthropic adapter response for: {{Escape(question)}}",
                 "llm_certainty":0.80
               }
               """;
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
