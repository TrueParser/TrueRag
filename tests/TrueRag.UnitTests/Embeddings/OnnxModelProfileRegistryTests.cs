using TrueRag.Core.Models;
using TrueRag.Embeddings;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OnnxModelProfileRegistryTests
{
    [Fact]
    public void GetRequiredProfile_ResolvesAllBuiltIns()
    {
        var registry = new OnnxModelProfileRegistry();

        var small = registry.GetRequiredProfile("bge-small-en-v1.5");
        var baseProfile = registry.GetRequiredProfile("bge-base-en-v1.5");
        var miniLm = registry.GetRequiredProfile("multi-qa-minilm-l6-cos-v1");

        Assert.Equal("BAAI/bge-small-en-v1.5", small.ModelId);
        Assert.Equal(768, baseProfile.Dimensions);
        Assert.Equal("sentence-transformers/multi-qa-MiniLM-L6-cos-v1", miniLm.ModelId);
    }
}
