using TrueRag.Embeddings.Onnx;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OnnxTextPreprocessorTests
{
    [Fact]
    public void Normalize_IsDeterministic_AndCompactsWhitespace()
    {
        var preprocessor = new OnnxTextPreprocessor();
        const string input = "  Hello\n\r   world   from   TrueRag  ";

        var first = preprocessor.Normalize(input, 10);
        var second = preprocessor.Normalize(input, 10);

        Assert.Equal("Hello world from TrueRag", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Normalize_Truncates_ToMaxTokens()
    {
        var preprocessor = new OnnxTextPreprocessor();

        var value = preprocessor.Normalize("a b c d", 2);

        Assert.Equal("a b", value);
    }
}
