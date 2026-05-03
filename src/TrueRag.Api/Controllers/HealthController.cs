using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Models;
using TrueRag.Api.ResourceGuard;
using TrueRag.Api.Services;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live()
        => Ok(new HealthLiveView("alive", DateTime.UtcNow));

    [HttpGet("ready")]
    public async Task<IActionResult> Ready([FromServices] IDependencyReadinessEvaluator readinessEvaluator, CancellationToken cancellationToken)
    {
        var result = await readinessEvaluator.EvaluateAsync(cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new HealthReadyView("ready", DateTime.UtcNow, result.Value!));
        }

        if (result.Error?.Type == ErrorType.Unavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new HealthReadyView(
                    "unhealthy",
                    DateTime.UtcNow,
                    new Dictionary<string, string> { ["storage"] = "unavailable" },
                    new Dictionary<string, string> { ["critical"] = result.Error.Message }));
        }

        return StatusCode(StatusCodes.Status500InternalServerError,
            new HealthReadyView(
                "unhealthy",
                DateTime.UtcNow,
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["unexpected"] = result.Error?.Message ?? "Unknown error" }));
    }

    [HttpGet("node-state")]
    public IActionResult GetNodeState([FromServices] IResourceMonitor resourceMonitor)
    {
        var state = resourceMonitor.Current;
        var payload = new
        {
            state = state.State.ToString().ToLowerInvariant(),
            reason = state.DegradationReason,
            memoryPercent = state.MemoryPercent,
            cpuPercent = state.CpuPercent,
            threadPoolQueueDepth = state.ThreadPoolQueueDepth,
            activeRequests = state.ActiveRequests,
            liveQueueDepth = state.LiveQueueDepth,
            drainCapacityRatio = state.DrainCapacityRatio,
            acceptedItemsPerSec = state.AcceptedItemsPerSec,
            drainedItemsPerSec = state.DrainedItemsPerSec,
            sampledAtUtc = state.SampledAtUtc
        };

        return state.State == NodeState.Overloaded
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, payload)
            : Ok(payload);
    }
}
