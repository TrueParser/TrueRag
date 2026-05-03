using Microsoft.AspNetCore.Http;

namespace TrueRag.Api.ResourceGuard;

public interface IApiAdmissionGuard
{
    ValueTask<bool> IsRequestAllowedAsync(HttpContext context, CancellationToken cancellationToken = default);
}
