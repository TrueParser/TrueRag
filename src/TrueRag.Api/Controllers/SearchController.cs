using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Helpers;
using TrueRag.Api.Services;
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
        [FromServices] IRetrievalApiService retrievalApiService,
        [FromBody] RetrievalQuery query,
        CancellationToken cancellationToken)
        => this.ToActionResult(await retrievalApiService.SearchVector(context, query, cancellationToken));

    [HttpPost("text")]
    public async Task<IActionResult> Text(
        [FromServices] IRequestContext context,
        [FromServices] IRetrievalApiService retrievalApiService,
        [FromBody] RetrievalQuery query,
        CancellationToken cancellationToken)
        => this.ToActionResult(await retrievalApiService.SearchText(context, query, cancellationToken));

    [HttpPost("hybrid")]
    public async Task<IActionResult> Hybrid(
        [FromServices] IRequestContext context,
        [FromServices] IRetrievalApiService retrievalApiService,
        [FromBody] RetrievalQuery query,
        CancellationToken cancellationToken)
        => this.ToActionResult(await retrievalApiService.SearchHybrid(context, query, cancellationToken));
}
