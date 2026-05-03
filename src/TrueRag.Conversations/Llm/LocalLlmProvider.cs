using TrueRag.Core.Models;

namespace TrueRag.Conversations.Llm;

internal sealed class LocalLlmProvider : BaseStubLlmProvider
{
    public LocalLlmProvider(ILlmResponseParser parser)
        : base(parser)
    {
    }

    public override string ProviderId => "local";

    protected override string BuildRawResponse(LlmCompletionRequest request)
    {
        var question = request.Messages.LastOrDefault(static message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;
        return $$"""
               {
                 "answer":"Local provider response based on question: {{Escape(question)}}",
                 "llm_certainty":0.78
               }
               """;
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
