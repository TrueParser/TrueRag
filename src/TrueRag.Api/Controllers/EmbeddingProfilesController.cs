using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Helpers;
using TrueRag.Api.Models;
using TrueRag.Api.Services;
using TrueRag.Core.Context;
using TrueRag.Core.Models;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("api/v1/embedding-profiles")]
public sealed class EmbeddingProfilesController : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(
        [FromServices] IRequestContext context,
        [FromServices] IEmbeddingProfileApiService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetCurrentAsync(context, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromServices] IRequestContext context,
        [FromServices] IEmbeddingProfileApiService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPendingAsync(context, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("migration-readiness")]
    public async Task<IActionResult> GetMigrationReadiness(
        [FromServices] IRequestContext context,
        [FromServices] IEmbeddingProfileApiService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetReadinessAsync(context, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("transitions")]
    public async Task<IActionResult> ProposeTransition(
        [FromServices] IRequestContext context,
        [FromServices] IEmbeddingProfileApiService service,
        [FromBody] EmbeddingProfileTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = new EmbeddingProfileTransitionProposal(
            context.TenantId,
            context.AppId,
            context.CollectionId,
            request.Provider,
            request.Model,
            request.Dimensions,
            request.MaxTokens,
            request.DistanceMetric,
            request.RequiresReembedding,
            request.ReembeddingCompleted,
            request.Version,
            request.Checksum,
            request.Notes);

        var result = await service.ProposeAsync(context, proposal, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("transitions/{transitionId}/reembedding-completed")]
    public async Task<IActionResult> MarkReembeddingCompleted(
        [FromServices] IEmbeddingProfileApiService service,
        [FromRoute] string transitionId,
        CancellationToken cancellationToken)
    {
        var result = await service.MarkReembeddingCompletedAsync(transitionId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("transitions/{transitionId}/activate")]
    public async Task<IActionResult> Activate(
        [FromServices] IEmbeddingProfileApiService service,
        [FromRoute] string transitionId,
        CancellationToken cancellationToken)
    {
        var result = await service.ActivateAsync(transitionId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("transitions/{transitionId}/rollback")]
    public async Task<IActionResult> Rollback(
        [FromServices] IEmbeddingProfileApiService service,
        [FromRoute] string transitionId,
        CancellationToken cancellationToken)
    {
        var result = await service.RollbackAsync(transitionId, cancellationToken);
        return this.ToActionResult(result);
    }
}

