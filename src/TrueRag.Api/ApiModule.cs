using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrueRag.Api.Context;
using TrueRag.Api.Extensions;
using TrueRag.Api.ResourceGuard;
using TrueRag.Core.Context;

namespace TrueRag.Api;

public static class ApiModule
{
    public static IServiceCollection AddTrueRagApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddControllers()
            .AddApplicationPart(typeof(ApiModule).Assembly);

        services.AddOptions<RequestContextOptions>()
            .BindConfiguration(RequestContextOptions.SectionName)
            .Validate(static options =>
                !string.IsNullOrWhiteSpace(options.TenantHeaderName) &&
                !string.IsNullOrWhiteSpace(options.AppHeaderName) &&
                !string.IsNullOrWhiteSpace(options.TenantClaimType) &&
                !string.IsNullOrWhiteSpace(options.AppClaimType),
                "RequestContext options must define tenant/app claim and header names.");
        services.AddOptions<ResourceGuardOptions>()
            .BindConfiguration(ResourceGuardOptions.SectionName);

        services.TryAddScoped<IRequestContextResolver, HttpRequestContextResolver>();
        services.TryAddScoped<IRequestContext>(serviceProvider =>
        {
            var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
            var context = httpContextAccessor.HttpContext
                ?? throw new RequestContextResolutionException("No active HTTP request context is available.");
            var resolver = serviceProvider.GetRequiredService<IRequestContextResolver>();
            return resolver.Resolve(context);
        });
        services.TryAddSingleton<IResourceMonitor, ResourceMonitor>();
        services.TryAddSingleton<IApiAdmissionGuard, ApiAdmissionGuard>();
        services.AddHostedService(sp => (ResourceMonitor)sp.GetRequiredService<IResourceMonitor>());

        services.AddTrueRagApiServices();
        return services;
    }
}
