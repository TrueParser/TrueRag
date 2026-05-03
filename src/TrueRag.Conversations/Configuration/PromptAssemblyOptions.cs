namespace TrueRag.Conversations.Configuration;

public sealed class PromptAssemblyOptions
{
    public const string SectionName = "PromptAssembly";

    public int DefaultTokenBudget { get; set; } = 3000;

    public int ReservedCompletionTokens { get; set; } = 700;

    public string SystemInstruction { get; set; } =
        "Answer only from provided context. If context is insufficient, explicitly say so.";
}
