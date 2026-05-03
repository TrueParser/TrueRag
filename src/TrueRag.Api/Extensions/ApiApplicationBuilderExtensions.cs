using Microsoft.AspNetCore.Builder;
using TrueRag.Api.Middleware;
using TrueRag.Api.ResourceGuard;

namespace TrueRag.Api.Extensions;

public static class ApiApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTrueRagApiPipeline(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ResourceGuardMiddleware>();
        app.UseMiddleware<TenantScopeGuardMiddleware>();
        return app;
    }
}
