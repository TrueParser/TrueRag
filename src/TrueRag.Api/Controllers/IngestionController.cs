using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Helpers;
using TrueRag.Api.Models;
using TrueRag.Api.Services;
using TrueRag.Core.Context;
using TrueRag.Core.Models;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
public sealed class IngestionController : ControllerBase
{
    [HttpPost("async")]
    public async Task<IActionResult> IngestAsync(
        [FromServices] IRequestContext context,
        [FromServices] IIngestionApiService ingestionApiService,
        [FromBody] IngestionRequestDto payload,
        CancellationToken cancellationToken)
    {
        var result = await ingestionApiService.IngestAsync(context, payload, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error?.Code is "queue_depth_exhausted" or "wal_backpressure_high")
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, result.Error);
            }

            return this.FromError(result.Error!);
        }

        var value = result.Value!;
        return Accepted(new IngestAsyncAcceptedView(
            value.NodeId,
            value.TenantId,
            value.AppId,
            value.CollectionId,
            value.WalPath,
            value.WalSegmentId,
            value.WalOffset,
            value.WalLength));
    }

    [HttpPost("sync")]
    public async Task<IActionResult> IngestSync(
        [FromServices] IRequestContext context,
        [FromServices] IIngestionApiService ingestionApiService,
        [FromBody] IngestionRequestDto payload,
        CancellationToken cancellationToken)
    {
        var result = await ingestionApiService.IngestSync(context, payload, cancellationToken);
        return this.ToActionResult(result);
    }
}
