namespace TrueRag.Embeddings.Configuration;

public sealed class ExternalEmbeddingResilienceOptions
{
    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRetries { get; set; } = 2;

    public int BaseDelayMilliseconds { get; set; } = 200;

    public int MaxJitterMilliseconds { get; set; } = 100;
}

public sealed class OpenAiEmbeddingOptions
{
    public const string SectionName = "Embeddings:OpenAI";

    public bool Enabled { get; set; }

    public string ProviderName { get; set; } = "openai";

    public string ApiKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } = "https://api.openai.com/v1/embeddings";

    public string Model { get; set; } = "text-embedding-3-small";

    public int Dimensions { get; set; } = 1536;

    public int MaxTokens { get; set; } = 8192;

    public ExternalEmbeddingResilienceOptions Resilience { get; set; } = new();
}
