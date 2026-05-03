namespace TrueRag.Conversations.Configuration;

public sealed class LlmProviderOptions
{
    public const string SectionName = "LlmProvider";

    public string DefaultProvider { get; set; } = "local";
}
