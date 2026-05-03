namespace TrueRag.Api.Models;

public sealed record RequestContextView(
    string TenantId,
    string AppId,
    string? UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> AllowedDocumentGroups);
