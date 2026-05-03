using System.Text;
using TrueRag.Core.Models;

namespace TrueRag.Conversations;

internal sealed class ConversationSummaryBuilder : IConversationSummaryBuilder
{
    public string Build(IReadOnlyCollection<ConversationMessage> messages)
    {
        if (messages.Count == 0)
        {
            return string.Empty;
        }

        const int maxLineLength = 180;
        const int maxMessages = 8;

        var selected = messages
            .OrderByDescending(static message => message.OccurredAtUtc)
            .Take(maxMessages)
            .OrderBy(static message => message.OccurredAtUtc)
            .ToArray();

        var builder = new StringBuilder();
        foreach (var message in selected)
        {
            var role = string.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role.Trim().ToLowerInvariant();
            var text = Normalize(message.Message, maxLineLength);
            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(role);
            builder.Append(": ");
            builder.Append(text);
        }

        return builder.ToString();
    }

    private static string Normalize(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength] + "...";
    }
}
