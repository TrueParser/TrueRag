using Microsoft.Extensions.Options;
using TrueRag.Conversations.Configuration;
using TrueRag.Core.Models;

namespace TrueRag.Conversations.PromptAssembly;

internal sealed class PromptAssemblyService : IPromptAssemblyService
{
    private readonly PromptAssemblyOptions _options;

    public PromptAssemblyService(IOptions<PromptAssemblyOptions> options)
    {
        _options = options.Value;
    }

    public PromptAssemblyResult Assemble(
        ConversationGenerateRequest request,
        ConversationThreadSnapshot snapshot)
    {
        var totalBudget = Math.Max(512, request.PromptTokenBudget ?? _options.DefaultTokenBudget);
        var promptBudget = Math.Max(256, totalBudget - Math.Max(128, _options.ReservedCompletionTokens));
        var budgetUsed = 0;
        var messages = new List<LlmMessage>();

        Add("system", _options.SystemInstruction, required: true);
        Add("system", BuildSummary(snapshot.State.Summary), required: false);
        Add("user", request.UserMessage, required: true);

        foreach (var context in request.RetrievedContext
                     .OrderByDescending(static item => item.Score ?? 0)
                     .ThenBy(static item => item.NodeId, StringComparer.Ordinal))
        {
            Add("system", $"[context:{context.NodeId}] {context.Text}", required: false);
        }

        foreach (var history in snapshot.Messages
                     .OrderByDescending(static item => item.OccurredAtUtc)
                     .Take(10)
                     .OrderBy(static item => item.OccurredAtUtc))
        {
            Add(history.Role, history.Message, required: false);
        }

        var estimated = messages.Sum(static message => EstimateTokens(message.Content));
        return new PromptAssemblyResult(messages, estimated, budgetUsed, promptBudget);

        void Add(string role, string content, bool required)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var text = Normalize(content);
            var tokens = EstimateTokens(text);
            if (budgetUsed + tokens <= promptBudget)
            {
                messages.Add(new LlmMessage(role, text));
                budgetUsed += tokens;
                return;
            }

            if (!required)
            {
                return;
            }

            var remaining = Math.Max(16, promptBudget - budgetUsed);
            var truncated = TruncateToTokens(text, remaining);
            if (!string.IsNullOrWhiteSpace(truncated))
            {
                messages.Add(new LlmMessage(role, truncated));
                budgetUsed += EstimateTokens(truncated);
            }
        }
    }

    private static string BuildSummary(string? summary)
        => string.IsNullOrWhiteSpace(summary) ? string.Empty : $"[summary] {summary}";

    private static string Normalize(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    internal static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words * 1.35));
    }

    private static string TruncateToTokens(string text, int tokenBudget)
    {
        if (tokenBudget <= 0)
        {
            return string.Empty;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        var allowedWords = Math.Max(1, (int)Math.Floor(tokenBudget / 1.35));
        if (allowedWords >= words.Length)
        {
            return text;
        }

        return string.Join(' ', words.Take(allowedWords)) + " ...";
    }
}
