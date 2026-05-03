using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Models;
using TrueRag.Core.Context;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/context")]
public sealed class ContextController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromServices] IRequestContext context)
    {
        return Ok(new RequestContextView(
            context.TenantId,
            context.AppId,
            context.UserId,
            context.Roles,
            context.AllowedDocumentGroups));
    }
}
