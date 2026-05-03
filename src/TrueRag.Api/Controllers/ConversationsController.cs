using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Contracts;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/conversations/threads/{threadId}")]
public sealed class ConversationsController : ControllerBase
{
    [HttpPost("turns")]
    public async Task<IActionResult> AddTurn(
        [FromRoute] string threadId,
        [FromServices] IRequestContext context,
        [FromServices] IConversationService conversationService,
        [FromBody] ConversationTurnInput input,
        CancellationToken cancellationToken)
    {
        var turn = new ConversationTurn(
            ThreadId: threadId,
            UserMessage: input.UserMessage,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            ActiveDocumentId: input.ActiveDocumentId,
            ActiveSectionPath: input.ActiveSectionPath);

        var result = await conversationService.AddTurnAsync(context, turn, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> GetThread(
        [FromRoute] string threadId,
        [FromQuery] int? take,
        [FromServices] IRequestContext context,
        [FromServices] IConversationService conversationService,
        CancellationToken cancellationToken)
    {
        var result = await conversationService.GetThreadAsync(context, threadId, take ?? 50, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromRoute] string threadId,
        [FromQuery] int? recentWindow,
        [FromServices] IRequestContext context,
        [FromServices] IConversationService conversationService,
        CancellationToken cancellationToken)
    {
        var result = await conversationService.RefreshThreadStateAsync(context, threadId, recentWindow ?? 12, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
