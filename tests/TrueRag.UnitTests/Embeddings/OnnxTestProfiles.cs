using TrueRag.Embeddings.Configuration;

namespace TrueRag.UnitTests.Embeddings;

internal static class OnnxTestProfiles
{
    public static OnnxProfileSelectionOptions CreateSelectionOptions(string defaultProfileName = "bge-small-en-v1.5")
        => new()
        {
            DefaultProfileName = defaultProfileName,
            ExpectedVectorDimensions = null,
            ScopedProfiles = []
        };
}
