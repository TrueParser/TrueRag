using Microsoft.AspNetCore.Mvc;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Ingestion.Execution;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
public sealed class IngestionController : ControllerBase
{
    [HttpPost("async")]
    public async Task<IActionResult> IngestAsync(
        [FromServices] IRequestContext context,
        [FromServices] IIngestionExecutionService executionService,
        [FromBody] IngestionRequestDto payload,
        CancellationToken cancellationToken)
    {
        var result = await executionService.IngestAsyncBuffered(context, payload, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Accepted(value: new
        {
            result.Value!.NodeId,
            result.Value.TenantId,
            result.Value.AppId,
            result.Value.WalPath,
            result.Value.WalSegmentId,
            result.Value.WalOffset,
            result.Value.WalLength
        });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> IngestSync(
        [FromServices] IRequestContext context,
        [FromServices] IIngestionExecutionService executionService,
        [FromBody] IngestionRequestDto payload,
        CancellationToken cancellationToken)
    {
        var result = await executionService.IngestSyncAsync(context, payload, cancellationToken);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
