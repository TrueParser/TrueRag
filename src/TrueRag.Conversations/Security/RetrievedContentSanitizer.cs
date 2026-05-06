namespace TrueRag.Conversations.Security;

internal static class RetrievedContentSanitizer
{
    private static readonly string[] DangerousPatterns =
    [
        "ignore previous instructions",
        "ignore system instruction",
        "disregard instructions",
        "system prompt",
        "developer message",
        "jailbreak",
        "reveal secrets",
        "override policy",
        "fabricate citation"
    ];

    public static SanitizedRetrievedContent Sanitize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SanitizedRetrievedContent(string.Empty, false, []);
        }

        var normalized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        var lowered = normalized.ToLowerInvariant();
        var matches = DangerousPatterns
            .Where(pattern => lowered.Contains(pattern, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (matches.Length == 0)
        {
            return new SanitizedRetrievedContent(normalized, false, []);
        }

        var sanitized = normalized;
        foreach (var pattern in matches)
        {
            sanitized = ReplaceCaseInsensitive(sanitized, pattern, "[redacted-injection]");
        }

        return new SanitizedRetrievedContent(sanitized, true, matches);
    }

    private static string ReplaceCaseInsensitive(string source, string search, string replacement)
    {
        var result = source;
        var index = result.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            result = result.Remove(index, search.Length).Insert(index, replacement);
            index = result.IndexOf(search, index + replacement.Length, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}

internal sealed record SanitizedRetrievedContent(
    string Text,
    bool HadInjectionSignals,
    IReadOnlyCollection<string> MatchedPatterns);

