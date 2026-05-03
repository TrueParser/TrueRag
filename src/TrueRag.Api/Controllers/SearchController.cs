using Microsoft.AspNetCore.Mvc;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/search")]
public sealed class SearchController : ControllerBase
{
    [HttpPost("vector")]
    public async Task<IActionResult> Vector(
        [FromServices] IRequestContext context,
        [FromServices] IRetrievalService retrievalService,
        [FromBody] RetrievalQuery query,
        CancellationToken cancellationToken)
    {
        var result = await retrievalService.SearchVectorAsync(context, query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("text")]
    public async Task<IActionResult> Text(
        [FromServices] IRequestContext context,
        [FromServices] IRetrievalService retrievalService,
        [FromBody] RetrievalQuery query,
        CancellationToken cancellationToken)
    {
        var result = await retrievalService.SearchTextAsync(context, query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("hybrid")]
    public async Task<IActionResult> Hybrid(
        [FromServices] IRequestContext context,
        [FromServices] IRetrievalService retrievalService,
        [FromBody] RetrievalQuery query,
        CancellationToken cancellationToken)
    {
        var result = await retrievalService.SearchHybridAsync(context, query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
