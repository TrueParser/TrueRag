using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Core.Models;
using TrueRag.Ingestion;
using TrueRag.Ingestion.Models;
using TrueRag.Ingestion.Normalization;

namespace TrueRag.UnitTests.Ingestion;

public sealed class IngestionNormalizationTests
{
    [Fact]
    public void Normalize_AutoDetects_HighFidelity_WhenStructurePresent()
    {
        var normalizer = CreateNormalizer();
        var payload = CreatePayload(
            fidelity: "auto",
            chunks:
            [
                new ChunkDto(
                    Id: "n1",
                    ParentId: "section-1",
                    LogicalPath: "Document/Section1/Paragraph1",
                    Type: "Paragraph",
                    Text: "hello",
                    BoundingBox: new BoundingBoxDto(1, 1, 2, 3, 4),
                    ReferencedNodeIds: ["n2"],
                    Vector: [0.1f, 0.2f])
            ]);

        var result = normalizer.Normalize(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(FidelityLevel.High, result.Value!.FidelityLevel);
        Assert.True(result.Value.Nodes.First().HasHierarchyMetadata);
        Assert.True(result.Value.Nodes.First().HasProvenanceMetadata);
    }

    [Fact]
    public void Normalize_AutoDetects_StandardFidelity_WhenOnlyFlatChunks()
    {
        var normalizer = CreateNormalizer();
        var payload = CreatePayload(
            fidelity: "auto",
            chunks:
            [
                new ChunkDto(
                    Id: "n1",
                    ParentId: null,
                    LogicalPath: null,
                    Type: "Paragraph",
                    Text: "flat",
                    BoundingBox: null,
                    ReferencedNodeIds: null,
                    Vector: [0.3f, 0.4f])
            ]);

        var result = normalizer.Normalize(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(FidelityLevel.Standard, result.Value!.FidelityLevel);
    }

    [Fact]
    public void Normalize_Respects_ExplicitFidelityOverride_WhenEnabled()
    {
        var normalizer = CreateNormalizer();
        var payload = CreatePayload(
            fidelity: "standard",
            chunks:
            [
                new ChunkDto(
                    Id: "n1",
                    ParentId: "p",
                    LogicalPath: "x",
                    Type: "Table",
                    Text: "table",
                    BoundingBox: new BoundingBoxDto(1, 1, 1, 1, 1),
                    ReferencedNodeIds: null,
                    Vector: [0.5f, 0.6f])
            ]);

        var result = normalizer.Normalize(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(FidelityLevel.Standard, result.Value!.FidelityLevel);
    }

    [Fact]
    public void Normalize_Ignores_ExplicitFidelityOverride_WhenDisabled()
    {
        var normalizer = CreateNormalizer(allowExplicitOverride: false);
        var payload = CreatePayload(
            fidelity: "standard",
            chunks:
            [
                new ChunkDto(
                    Id: "n1",
                    ParentId: "p",
                    LogicalPath: "x",
                    Type: "Table",
                    Text: "table",
                    BoundingBox: new BoundingBoxDto(1, 1, 1, 1, 1),
                    ReferencedNodeIds: null,
                    Vector: [0.5f, 0.6f])
            ]);

        var result = normalizer.Normalize(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(FidelityLevel.High, result.Value!.FidelityLevel);
    }

    [Fact]
    public void Normalize_Fails_WhenChunkVectorMissing()
    {
        var normalizer = CreateNormalizer();
        var payload = CreatePayload(
            fidelity: "auto",
            chunks:
            [
                new ChunkDto(
                    Id: "n1",
                    ParentId: null,
                    LogicalPath: null,
                    Type: "Paragraph",
                    Text: "bad",
                    BoundingBox: null,
                    ReferencedNodeIds: null,
                    Vector: [])
            ]);

        var result = normalizer.Normalize(payload);

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.chunk_vector_required", result.Error!.Code);
    }

    [Fact]
    public void Normalize_Fails_WhenAllowedDocumentGroupsMissing()
    {
        var normalizer = CreateNormalizer();
        var payload = new IngestionRequestDto(
            DocumentId: "doc-1",
            DocumentGroupId: "group-1",
            VersionNumber: "1.0",
            AllowedDocumentGroups: [],
            Fidelity: "auto",
            Chunks:
            [
                new ChunkDto(
                    Id: "n1",
                    ParentId: null,
                    LogicalPath: null,
                    Type: "Paragraph",
                    Text: "text",
                    BoundingBox: null,
                    ReferencedNodeIds: null,
                    Vector: [0.1f])
            ]);

        var result = normalizer.Normalize(payload);

        Assert.True(result.IsFailure);
        Assert.Equal("ingestion.allowed_document_groups_required", result.Error!.Code);
    }

    private static IIngestionNormalizer CreateNormalizer(bool allowExplicitOverride = true)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IngestionFidelity:DefaultMode"] = "auto",
                ["IngestionFidelity:AllowExplicitOverride"] = allowExplicitOverride.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddTrueRagIngestion();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IIngestionNormalizer>();
    }

    private static IngestionRequestDto CreatePayload(string fidelity, IReadOnlyCollection<ChunkDto> chunks)
    {
        return new IngestionRequestDto(
            DocumentId: "doc-1",
            DocumentGroupId: "group-1",
            VersionNumber: "1.0",
            AllowedDocumentGroups: ["legal"],
            Fidelity: fidelity,
            Chunks: chunks);
    }
}
