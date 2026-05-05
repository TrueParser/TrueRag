namespace TrueRag.Embeddings.Configuration;

public sealed class OnnxEmbeddingOptions
{
    public const string SectionName = "Embeddings:Onnx";

    public string ProviderName { get; set; } = "onnx";

    public string ModelId { get; set; } = "BAAI/bge-small-en-v1.5";

    public int Dimensions { get; set; } = 384;

    public int MaxTokens { get; set; } = 512;

    public string DistanceMetric { get; set; } = "Cosine";

    public string ModelArtifactsPath { get; set; } = "models/onnx/bge-small-en-v1.5";

    public string ModelFileName { get; set; } = "model.onnx";

    public int MaxBatchSize { get; set; } = 16;

    public bool EnableWarmup { get; set; } = true;

    public int WarmupTimeoutSeconds { get; set; } = 10;

    public int InferenceTimeoutSeconds { get; set; } = 15;
}
