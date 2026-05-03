using TrueRag.Storage;

namespace TrueRag.UnitTests.Storage;

public sealed class TableProjectionFormatterTests
{
    [Fact]
    public void FormatForPrompt_WhenArrayOfObjects_ProducesMarkdownTable()
    {
        const string payload = """
                               [{"quarter":"Q1","revenue":100.5},{"quarter":"Q2","revenue":210}]
                               """;

        var result = TableProjectionFormatter.FormatForPrompt(payload);

        Assert.Contains("[Table Context]", result, StringComparison.Ordinal);
        Assert.Contains("| quarter | revenue |", result, StringComparison.Ordinal);
        Assert.Contains("| Q1 | 100.5 |", result, StringComparison.Ordinal);
        Assert.Contains("| Q2 | 210 |", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatForPrompt_WhenObject_ProducesJsonFence()
    {
        const string payload = """{"headers":["quarter","revenue"],"rows":[["Q1",100.5]]}""";

        var result = TableProjectionFormatter.FormatForPrompt(payload);

        Assert.Contains("[Table Context]", result, StringComparison.Ordinal);
        Assert.Contains("```json", result, StringComparison.Ordinal);
        Assert.Contains("\"headers\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatForPrompt_WhenInvalidJson_ProducesTextFence()
    {
        const string payload = "Q1 | 100.5";

        var result = TableProjectionFormatter.FormatForPrompt(payload);

        Assert.Contains("[Table Context]", result, StringComparison.Ordinal);
        Assert.Contains("```text", result, StringComparison.Ordinal);
        Assert.Contains(payload, result, StringComparison.Ordinal);
    }
}
