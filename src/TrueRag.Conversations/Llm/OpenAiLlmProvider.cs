using TrueRag.Core.Models;

namespace TrueRag.Conversations.Llm;

internal sealed class OpenAiLlmProvider : BaseStubLlmProvider
{
    public OpenAiLlmProvider(ILlmResponseParser parser)
        : base(parser)
    {
    }

    public override string ProviderId => "openai";

    protected override string BuildRawResponse(LlmCompletionRequest request)
    {
        var question = request.Messages.LastOrDefault(static message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;
        var escaped = Escape(question);
        return
            "{" +
            "\"answer\":\"OpenAI adapter response for: " + escaped + "\"," +
            "\"tool_calls\":[{\"id\":\"tool-1\",\"name\":\"citation_lookup\",\"arguments\":{\"query\":\"" + escaped + "\"}}]," +
            "\"llm_certainty\":0.81" +
            "}";
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
