namespace TrueRag.Api.Context;

public sealed class RequestContextOptions
{
    public const string SectionName = "RequestContext";

    public string TenantHeaderName { get; set; } = "X-Tenant-Id";

    public string AppHeaderName { get; set; } = "X-App-Id";

    public string CollectionHeaderName { get; set; } = "X-Collection-Id";

    public string TenantClaimType { get; set; } = "tenant_id";

    public string AppClaimType { get; set; } = "app_id";

    public string CollectionClaimType { get; set; } = "collection_id";

    public string CollectionIdPattern { get; set; } = "^[a-zA-Z0-9._:-]{1,128}$";

    public bool EnableCollectionScopeAuthorization { get; set; } = true;

    public string UserIdClaimType { get; set; } = "sub";

    public string RoleClaimType { get; set; } = "role";

    public string AllowedDocumentGroupClaimType { get; set; } = "allowed_document_group";
}
