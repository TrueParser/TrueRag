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
            "\"claims\":[{\"claim_id\":\"c1\",\"text\":\"OpenAI adapter response for: " + escaped + "\",\"citation_ids\":[\"cit-1\"]}]," +
            "\"citations\":[{\"citation_id\":\"cit-1\",\"node_id\":\"n1\",\"document_id\":\"doc-1\",\"section_path\":\"Section/1\",\"page_number\":1,\"support_score\":0.8}]," +
            "\"grounding_status\":\"grounded\"," +
            "\"insufficiency_reason\":null," +
            "\"confidence\":0.81," +
            "\"tool_calls\":[{\"id\":\"tool-1\",\"name\":\"citation_lookup\",\"arguments\":{\"query\":\"" + escaped + "\"}}]," +
            "\"llm_certainty\":0.81" +
            "}";
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
