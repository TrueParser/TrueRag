using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Contracts;
using TrueRag.Api.Helpers;
using TrueRag.Api.Services;
using TrueRag.Core.Context;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/conversations/threads/{threadId}")]
public sealed class ConversationsController : ControllerBase
{
    [HttpPost("turns")]
    public async Task<IActionResult> AddTurn(
        [FromRoute] string threadId,
        [FromServices] IRequestContext context,
        [FromServices] IConversationApiService conversationApiService,
        [FromBody] ConversationTurnInput input,
        CancellationToken cancellationToken)
        => this.ToActionResult(await conversationApiService.AddTurn(context, threadId, input, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> GetThread(
        [FromRoute] string threadId,
        [FromQuery] int? take,
        [FromServices] IRequestContext context,
        [FromServices] IConversationApiService conversationApiService,
        CancellationToken cancellationToken)
        => this.ToActionResult(await conversationApiService.GetThread(context, threadId, take, cancellationToken));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromRoute] string threadId,
        [FromQuery] int? recentWindow,
        [FromServices] IRequestContext context,
        [FromServices] IConversationApiService conversationApiService,
        CancellationToken cancellationToken)
        => this.ToActionResult(await conversationApiService.RefreshThread(context, threadId, recentWindow, cancellationToken));
}
