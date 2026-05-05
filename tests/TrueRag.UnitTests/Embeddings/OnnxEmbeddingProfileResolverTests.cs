using Microsoft.Extensions.Options;
using TrueRag.Core.Models;
using TrueRag.Embeddings;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OnnxEmbeddingProfileResolverTests
{
    [Fact]
    public async Task ResolveActiveDescriptorAsync_UsesScopedOverride()
    {
        var resolver = new OnnxEmbeddingProfileResolver(
            new OnnxModelProfileRegistry(),
            new StaticOptionsMonitor<OnnxProfileSelectionOptions>(new OnnxProfileSelectionOptions
            {
                DefaultProfileName = "bge-small-en-v1.5",
                ScopedProfiles =
                [
                    new EmbeddingScopeProfileSelection
                    {
                        TenantId = "tenant-1",
                        AppId = "app-1",
                        CollectionId = "collection-1",
                        ProfileName = "bge-base-en-v1.5"
                    }
                ]
            }),
            []);

        var descriptor = await resolver.ResolveActiveDescriptorAsync("tenant-1", "app-1", "collection-1");

        Assert.Equal("BAAI/bge-base-en-v1.5", descriptor.Model);
        Assert.Equal(768, descriptor.Dimensions);
    }

    [Fact]
    public async Task ResolveActiveDescriptorAsync_Throws_ForDimensionMismatch()
    {
        var resolver = new OnnxEmbeddingProfileResolver(
            new OnnxModelProfileRegistry(),
            new StaticOptionsMonitor<OnnxProfileSelectionOptions>(new OnnxProfileSelectionOptions
            {
                DefaultProfileName = "bge-base-en-v1.5",
                ExpectedVectorDimensions = 384
            }),
            []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveActiveDescriptorAsync("tenant", "app", "collection"));
    }
}
