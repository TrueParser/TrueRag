namespace TrueRag.Api.Models;

public sealed record HealthReadyView(
    string Status,
    DateTime TimestampUtc,
    IReadOnlyDictionary<string, string> Dependencies,
    IReadOnlyDictionary<string, string>? Failures = null);
