using TrueRag.Embeddings.Configuration;

namespace TrueRag.Embeddings.Onnx;

internal interface IOnnxModelProfileRegistry
{
    OnnxModelProfile GetRequiredProfile(string profileName);

    OnnxModelProfile GetDefaultProfile();

    OnnxModelProfile GetRequiredProfileByModelId(string modelId);

    IReadOnlyCollection<OnnxModelProfile> GetAllProfiles();
}

internal sealed class OnnxModelProfileRegistry : IOnnxModelProfileRegistry
{
    private static readonly IReadOnlyDictionary<string, OnnxModelProfile> BuiltInProfiles =
        new Dictionary<string, OnnxModelProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["bge-small-en-v1.5"] = new()
            {
                Name = "bge-small-en-v1.5",
                ModelId = "BAAI/bge-small-en-v1.5",
                Dimensions = 384,
                MaxTokens = 512,
                DistanceMetric = TrueRag.Core.Models.EmbeddingDistanceMetric.Cosine,
                ModelArtifactsPath = "models/onnx/bge-small-en-v1.5",
                ModelFileName = "model.onnx"
            },
            ["bge-base-en-v1.5"] = new()
            {
                Name = "bge-base-en-v1.5",
                ModelId = "BAAI/bge-base-en-v1.5",
                Dimensions = 768,
                MaxTokens = 512,
                DistanceMetric = TrueRag.Core.Models.EmbeddingDistanceMetric.Cosine,
                ModelArtifactsPath = "models/onnx/bge-base-en-v1.5",
                ModelFileName = "model.onnx"
            },
            ["multi-qa-minilm-l6-cos-v1"] = new()
            {
                Name = "multi-qa-minilm-l6-cos-v1",
                ModelId = "sentence-transformers/multi-qa-MiniLM-L6-cos-v1",
                Dimensions = 384,
                MaxTokens = 512,
                DistanceMetric = TrueRag.Core.Models.EmbeddingDistanceMetric.Cosine,
                ModelArtifactsPath = "models/onnx/multi-qa-minilm-l6-cos-v1",
                ModelFileName = "model.onnx"
            }
        };

    public OnnxModelProfile GetRequiredProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new InvalidOperationException("Embedding profile name is required.");
        }

        if (BuiltInProfiles.TryGetValue(profileName, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException($"Embedding profile '{profileName}' is not supported.");
    }

    public OnnxModelProfile GetDefaultProfile() => GetRequiredProfile("bge-small-en-v1.5");

    public OnnxModelProfile GetRequiredProfileByModelId(string modelId)
    {
        var profile = BuiltInProfiles.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.ModelId, modelId, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            throw new InvalidOperationException($"Embedding model id '{modelId}' is not supported.");
        }

        return profile;
    }

    public IReadOnlyCollection<OnnxModelProfile> GetAllProfiles() => BuiltInProfiles.Values.ToArray();
}
