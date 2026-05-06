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
                 "claims":[{"claim_id":"c1","text":"Anthropic adapter response for: {{Escape(question)}}","citation_ids":["cit-1"]}],
                 "citations":[{"citation_id":"cit-1","node_id":"n1","document_id":"doc-1","section_path":"Section/1","page_number":1,"support_score":0.80}],
                 "grounding_status":"grounded",
                 "insufficiency_reason":null,
                 "confidence":0.80,
                 "llm_certainty":0.80
               }
               """;
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
