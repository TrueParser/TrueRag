using TrueRag.Core.Models;
using TrueRag.Core.Validation;

namespace TrueRag.UnitTests.Core;

public sealed class EmbeddingContractValidatorTests
{
    [Fact]
    public void ValidateDescriptor_AllowsValidDescriptor()
    {
        var descriptor = new EmbeddingModelDescriptor("onnx", "bge-small", 384, 512, EmbeddingDistanceMetric.Cosine);

        EmbeddingContractValidator.ValidateDescriptor(descriptor);
    }

    [Fact]
    public void ValidateDescriptor_RejectsInvalidDimensions()
    {
        var descriptor = new EmbeddingModelDescriptor("onnx", "bge-small", 0, 512, EmbeddingDistanceMetric.Cosine);

        Assert.Throws<ArgumentOutOfRangeException>(() => EmbeddingContractValidator.ValidateDescriptor(descriptor));
    }

    [Fact]
    public void ValidateRequest_RejectsEmptyBatch()
    {
        var descriptor = new EmbeddingModelDescriptor("onnx", "bge-small", 384, 512, EmbeddingDistanceMetric.Cosine);
        var request = new EmbedBatchRequest([], descriptor);

        Assert.Throws<ArgumentException>(() => EmbeddingContractValidator.ValidateRequest(request));
    }

    [Fact]
    public void ValidateRequest_RejectsBlankText()
    {
        var descriptor = new EmbeddingModelDescriptor("onnx", "bge-small", 384, 512, EmbeddingDistanceMetric.Cosine);
        var request = new EmbedTextRequest(new EmbeddingInput("   "), descriptor);

        Assert.Throws<ArgumentException>(() => EmbeddingContractValidator.ValidateRequest(request));
    }
}
