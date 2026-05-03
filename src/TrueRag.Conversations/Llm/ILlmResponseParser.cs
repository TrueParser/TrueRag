using TrueRag.Core.Models;

namespace TrueRag.Conversations.Llm;

internal interface ILlmResponseParser
{
    LlmCompletionResponse Parse(string provider, string rawText, int promptTokensEstimate);
}
