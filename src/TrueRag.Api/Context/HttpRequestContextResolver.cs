using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TrueRag.Core.Context;

namespace TrueRag.Api.Context;

internal sealed class HttpRequestContextResolver : IRequestContextResolver
{
    private static readonly string[] SplitSeparators = [",", ";"];

    private readonly RequestContextOptions _options;

    public HttpRequestContextResolver(IOptions<RequestContextOptions> options)
    {
        _options = options.Value;
    }

    public IRequestContext Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var principal = httpContext.User;

        var tenantId = ReadRequiredValue(
            principal,
            _options.TenantClaimType,
            httpContext,
            _options.TenantHeaderName,
            "tenant");

        var appId = ReadRequiredValue(
            principal,
            _options.AppClaimType,
            httpContext,
            _options.AppHeaderName,
            "app");

        var userId = principal.FindFirstValue(_options.UserIdClaimType);
        var roles = ReadValues(principal, _options.RoleClaimType);
        var allowedGroups = ReadValues(principal, _options.AllowedDocumentGroupClaimType);

        return new RequestContext(tenantId, appId, userId, roles, allowedGroups);
    }

    private static string ReadRequiredValue(
        ClaimsPrincipal principal,
        string claimType,
        HttpContext context,
        string headerName,
        string fieldName)
    {
        var value = principal.FindFirstValue(claimType);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = context.Request.Headers[headerName].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestContextResolutionException(
                $"Missing required request context field '{fieldName}'.");
        }

        return value.Trim();
    }

    private static IReadOnlyCollection<string> ReadValues(ClaimsPrincipal principal, string claimType)
    {
        var values = principal.FindAll(claimType)
            .SelectMany(static claim => claim.Value.Split(SplitSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values;
    }
}