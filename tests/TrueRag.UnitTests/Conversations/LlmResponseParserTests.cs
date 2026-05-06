using TrueRag.Conversations.Llm;
using TrueRag.Core.Models;

namespace TrueRag.UnitTests.Conversations;

public sealed class LlmResponseParserTests
{
    [Fact]
    public void Parse_ExtractsToolCalls_AndCertainty_FromStructuredJson()
    {
        var parser = new LlmResponseParser();
        var raw =
            """
            {
              "answer":"Clause found in section 8.",
              "llm_certainty":0.84,
              "claims":[{"claim_id":"c1","text":"Clause found in section 8.","citation_ids":["cit-1"]}],
              "citations":[{"citation_id":"cit-1","node_id":"n1","document_id":"doc-1","section_path":"S/8","page_number":8,"support_score":0.84,"span_id":"sp-1","start_offset":10,"end_offset":20,"quote":"section 8 text"}],
              "grounding_status":"grounded",
              "insufficiency_reason":null,
              "confidence":0.84,
              "tool_calls":[
                {"id":"tc1","name":"lookup","arguments":{"nodeId":"n1"}}
              ]
            }
            """;

        var parsed = parser.Parse("openai", raw, 120);

        Assert.Equal("openai", parsed.Provider);
        Assert.Equal("Clause found in section 8.", parsed.Text);
        Assert.Single(parsed.ToolCalls);
        Assert.Equal("lookup", parsed.ToolCalls.First().Name);
        Assert.Equal(0.84, parsed.LlmCertainty);
        Assert.NotNull(parsed.GroundedResponse);
        Assert.Equal(GroundingStatus.Grounded, parsed.GroundedResponse!.GroundingStatus);
        Assert.Equal("sp-1", parsed.GroundedResponse.Citations.First().SpanId);
        Assert.Equal(10, parsed.GroundedResponse.Citations.First().StartOffset);
        Assert.Equal(20, parsed.GroundedResponse.Citations.First().EndOffset);
    }

    [Fact]
    public void Parse_WhenSchemaFieldsMissing_ReturnsSchemaValidationError()
    {
        var parser = new LlmResponseParser();
        var raw =
            """
            {
              "answer":"Clause found in section 8."
            }
            """;

        var parsed = parser.Parse("openai", raw, 120);

        Assert.Null(parsed.GroundedResponse);
        Assert.Equal("schema_invalid.claims_missing", parsed.SchemaValidationErrorCode);
    }
}
