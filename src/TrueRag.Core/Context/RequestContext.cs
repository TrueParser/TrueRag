namespace TrueRag.Core.Context;

public sealed record RequestContext(
    string TenantId,
    string AppId,
    string? UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> AllowedDocumentGroups) : IRequestContext;