using Microsoft.AspNetCore.Builder;

namespace TrueRag.Api.Context;

public static class RequestContextApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTrueRagRequestContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestContextMiddleware>();
    }
}