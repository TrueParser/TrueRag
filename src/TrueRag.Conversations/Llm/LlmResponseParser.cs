using System.Text.Json;
using TrueRag.Core.Models;

namespace TrueRag.Conversations.Llm;

internal sealed class LlmResponseParser : ILlmResponseParser
{
    public LlmCompletionResponse Parse(string provider, string rawText, int promptTokensEstimate)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new LlmCompletionResponse(
                Text: string.Empty,
                ToolCalls: [],
                Usage: new LlmUsage(promptTokensEstimate, 0, promptTokensEstimate),
                Provider: provider,
                LlmCertainty: null);
        }

        var trimmed = rawText.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;

                var text = root.TryGetProperty("answer", out var answerElement)
                    ? answerElement.GetString() ?? string.Empty
                    : rawText;

                var certainty = root.TryGetProperty("llm_certainty", out var certaintyElement) && certaintyElement.TryGetDouble(out var c)
                    ? c
                    : (double?)null;

                var toolCalls = new List<LlmToolCall>();
                if (root.TryGetProperty("tool_calls", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in toolsElement.EnumerateArray())
                    {
                        var id = tool.TryGetProperty("id", out var idElement)
                            ? idElement.GetString() ?? Guid.NewGuid().ToString("N")
                            : Guid.NewGuid().ToString("N");
                        var name = tool.TryGetProperty("name", out var nameElement)
                            ? nameElement.GetString() ?? "unknown_tool"
                            : "unknown_tool";
                        var args = tool.TryGetProperty("arguments", out var argsElement)
                            ? argsElement.GetRawText()
                            : "{}";
                        toolCalls.Add(new LlmToolCall(id, name, args));
                    }
                }

                var completionTokens = Math.Max(1, PromptAssembly.PromptAssemblyService.EstimateTokens(text));
                return new LlmCompletionResponse(
                    Text: text,
                    ToolCalls: toolCalls,
                    Usage: new LlmUsage(promptTokensEstimate, completionTokens, promptTokensEstimate + completionTokens),
                    Provider: provider,
                    LlmCertainty: certainty);
            }
            catch
            {
            }
        }

        var fallbackTokens = Math.Max(1, PromptAssembly.PromptAssemblyService.EstimateTokens(rawText));
        return new LlmCompletionResponse(
            Text: rawText,
            ToolCalls: [],
            Usage: new LlmUsage(promptTokensEstimate, fallbackTokens, promptTokensEstimate + fallbackTokens),
            Provider: provider,
            LlmCertainty: null);
    }
}
