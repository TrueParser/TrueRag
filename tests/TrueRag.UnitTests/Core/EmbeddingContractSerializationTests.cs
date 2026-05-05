using System.Text.Json;
using TrueRag.Core.Models;

namespace TrueRag.UnitTests.Core;

public sealed class EmbeddingContractSerializationTests
{
    [Fact]
    public void EmbeddingModelDescriptor_RoundTrips_WithExpectedValues()
    {
        var descriptor = new EmbeddingModelDescriptor(
            "onnx",
            "bge-small-en-v1.5",
            384,
            512,
            EmbeddingDistanceMetric.Cosine,
            "v1",
            "abc123");

        var json = JsonSerializer.Serialize(descriptor);
        var roundTrip = JsonSerializer.Deserialize<EmbeddingModelDescriptor>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(descriptor, roundTrip);
    }

    [Fact]
    public void EmbedBatchRequest_RoundTrips_WithMetadataAndContext()
    {
        var descriptor = new EmbeddingModelDescriptor("onnx", "bge-small", 384, 512, EmbeddingDistanceMetric.Cosine);
        var request = new EmbedBatchRequest(
            [
                new EmbeddingInput("first text", new Dictionary<string, string> { ["source"] = "ingest" }),
                new EmbeddingInput("second text")
            ],
            descriptor,
            new EmbeddingGenerationContext("t1", "a1", "c1", "corr-1", new Dictionary<string, string> { ["lane"] = "async" }));

        var json = JsonSerializer.Serialize(request);
        var roundTrip = JsonSerializer.Deserialize<EmbedBatchRequest>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(request.Model, roundTrip.Model);
        Assert.Equal(2, roundTrip.Inputs.Count);
        Assert.Equal("first text", roundTrip.Inputs.First().Text);
        Assert.Equal("t1", roundTrip.Context?.TenantId);
        Assert.Equal("c1", roundTrip.Context?.CollectionId);
    }
}
