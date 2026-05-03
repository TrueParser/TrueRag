using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Contracts;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/rag")]
public sealed class RagController : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromServices] IRequestContext context,
        [FromServices] IConversationService conversationService,
        [FromBody] RagGenerateInput input,
        CancellationToken cancellationToken)
    {
        var request = new ConversationGenerateRequest(
            ThreadId: input.ThreadId,
            UserMessage: input.UserMessage,
            RetrievedContext: input.RetrievedContext ?? [],
            Provider: input.Provider,
            PromptTokenBudget: input.PromptTokenBudget);

        var result = await conversationService.GenerateReplyAsync(context, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
