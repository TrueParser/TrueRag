using TrueRag.Conversations.Llm;

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
    }
}
