using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TrueRag.Storage;

internal static class TableProjectionFormatter
{
    public static string FormatForPrompt(string rawTablePayload)
    {
        if (string.IsNullOrWhiteSpace(rawTablePayload))
        {
            return "[Table Context]\n[]";
        }

        try
        {
            using var document = JsonDocument.Parse(rawTablePayload);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => FormatArray(document.RootElement),
                JsonValueKind.Object => FormatObject(document.RootElement),
                _ => WrapRaw(rawTablePayload)
            };
        }
        catch (JsonException)
        {
            return WrapRaw(rawTablePayload);
        }
    }

    private static string FormatArray(JsonElement arrayElement)
    {
        var items = arrayElement.EnumerateArray().ToArray();
        if (items.Length == 0)
        {
            return "[Table Context]\n[]";
        }

        if (!items.All(static element => element.ValueKind is JsonValueKind.Object))
        {
            return "[Table Context]\n```json\n" + arrayElement.GetRawText() + "\n```";
        }

        var headerSet = new HashSet<string>(StringComparer.Ordinal);
        var headers = new List<string>();

        foreach (var item in items)
        {
            foreach (var property in item.EnumerateObject())
            {
                if (headerSet.Add(property.Name))
                {
                    headers.Add(property.Name);
                }
            }
        }

        if (headers.Count == 0)
        {
            return "[Table Context]\n```json\n" + arrayElement.GetRawText() + "\n```";
        }

        var markdown = new StringBuilder();
        markdown.AppendLine("[Table Context]");
        markdown.AppendLine("| " + string.Join(" | ", headers) + " |");
        markdown.AppendLine("| " + string.Join(" | ", headers.Select(static _ => "---")) + " |");

        foreach (var item in items)
        {
            var values = headers.Select(header =>
            {
                if (!item.TryGetProperty(header, out var value))
                {
                    return string.Empty;
                }

                return EscapeMarkdown(ToCellText(value));
            });

            markdown.AppendLine("| " + string.Join(" | ", values) + " |");
        }

        return markdown.ToString().TrimEnd();
    }

    private static string FormatObject(JsonElement objectElement) =>
        "[Table Context]\n```json\n" + objectElement.GetRawText() + "\n```";

    private static string WrapRaw(string payload) =>
        "[Table Context]\n```text\n" + payload + "\n```";

    private static string ToCellText(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.TryGetDecimal(out var number)
                ? number.ToString(CultureInfo.InvariantCulture)
                : value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };

    private static string EscapeMarkdown(string input) =>
        input.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
