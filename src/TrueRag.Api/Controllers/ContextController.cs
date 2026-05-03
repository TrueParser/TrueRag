using Microsoft.AspNetCore.Mvc;
using TrueRag.Core.Context;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/context")]
public sealed class ContextController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromServices] IRequestContext context)
    {
        return Ok(new
        {
            context.TenantId,
            context.AppId,
            context.UserId,
            Roles = context.Roles,
            AllowedDocumentGroups = context.AllowedDocumentGroups
        });
    }
}
