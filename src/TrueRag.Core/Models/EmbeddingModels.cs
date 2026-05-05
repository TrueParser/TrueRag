namespace TrueRag.Core.Models;

public enum EmbeddingDistanceMetric
{
    Cosine = 0,
    DotProduct = 1,
    Euclidean = 2
}

[Flags]
public enum EmbeddingCapabilityFlags
{
    None = 0,
    SingleText = 1 << 0,
    BatchText = 1 << 1,
    InternalExecution = 1 << 2,
    ExternalExecution = 1 << 3
}

public sealed record EmbeddingModelDescriptor(
    string Provider,
    string Model,
    int Dimensions,
    int MaxTokens,
    EmbeddingDistanceMetric DistanceMetric,
    string? Version = null,
    string? Checksum = null);

public sealed record EmbeddingInput(
    string Text,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record EmbeddingUsage(
    int InputTokens,
    int BillableTokens = 0);

public sealed record EmbeddingGenerationContext(
    string? TenantId,
    string? AppId,
    string? CollectionId,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record EmbedTextRequest(
    EmbeddingInput Input,
    EmbeddingModelDescriptor Model,
    EmbeddingGenerationContext? Context = null);

public sealed record EmbedBatchRequest(
    IReadOnlyCollection<EmbeddingInput> Inputs,
    EmbeddingModelDescriptor Model,
    EmbeddingGenerationContext? Context = null);

public sealed record EmbedTextResult(
    float[] Vector,
    EmbeddingModelDescriptor Model,
    EmbeddingUsage Usage,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record EmbedBatchResult(
    IReadOnlyCollection<float[]> Vectors,
    EmbeddingModelDescriptor Model,
    EmbeddingUsage Usage,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record EmbeddingProviderCapabilities(
    EmbeddingCapabilityFlags Flags,
    int MaxBatchSize,
    IReadOnlyCollection<EmbeddingDistanceMetric> SupportedDistanceMetrics);
