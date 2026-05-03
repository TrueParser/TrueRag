using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Contracts;
using TrueRag.Api.Helpers;
using TrueRag.Api.Services;
using TrueRag.Core.Context;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/rag")]
public sealed class RagController : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromServices] IRequestContext context,
        [FromServices] IConversationApiService conversationApiService,
        [FromBody] RagGenerateInput input,
        CancellationToken cancellationToken)
        => this.ToActionResult(await conversationApiService.Generate(context, input, cancellationToken));
}
