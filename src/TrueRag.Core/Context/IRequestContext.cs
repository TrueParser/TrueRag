namespace TrueRag.Core.Context;

public interface IRequestContext
{
    string TenantId { get; }
    string AppId { get; }
    string CollectionId { get; }
    string? UserId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> AllowedDocumentGroups { get; }
}
