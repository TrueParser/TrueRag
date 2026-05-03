using Microsoft.AspNetCore.Http;
using TrueRag.Core.Context;

namespace TrueRag.Api.Context;

public interface IRequestContextResolver
{
    IRequestContext Resolve(HttpContext httpContext);
}