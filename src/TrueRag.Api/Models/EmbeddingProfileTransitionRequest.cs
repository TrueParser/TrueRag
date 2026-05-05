using TrueRag.Core.Models;

namespace TrueRag.Api.Models;

public sealed record EmbeddingProfileTransitionRequest(
    string Provider,
    string Model,
    int Dimensions,
    int MaxTokens,
    EmbeddingDistanceMetric DistanceMetric,
    bool RequiresReembedding,
    bool ReembeddingCompleted,
    string? Version = null,
    string? Checksum = null,
    string? Notes = null);

