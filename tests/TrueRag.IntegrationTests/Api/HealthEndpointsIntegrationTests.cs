using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Controllers;
using TrueRag.Api.ResourceGuard;
using TrueRag.Api.Services;
using TrueRag.Core.Primitives;

namespace TrueRag.IntegrationTests.Api;

public sealed class HealthEndpointsIntegrationTests
{
    [Fact]
    public async Task Ready_Returns503_OnDependencyFailure()
    {
        var controller = new HealthController();

        var result = await controller.Ready(new FakeEvaluator(Result<IReadOnlyDictionary<string, string>>.Failure(
            new Error("health.readiness_failed", "down", ErrorType.Unavailable))), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
    }

    [Fact]
    public void NodeState_Returns200_ForDegraded()
    {
        var controller = new HealthController();
        var result = controller.GetNodeState(new FakeMonitor(new ResourceSnapshot(
            70, 70, 5, 200, 1.2, 40, 35, 300, NodeState.Degraded, "recovering", DateTime.UtcNow)));

        Assert.IsType<OkObjectResult>(result);
    }

    private sealed class FakeEvaluator : IDependencyReadinessEvaluator
    {
        private readonly Result<IReadOnlyDictionary<string, string>> _result;

        public FakeEvaluator(Result<IReadOnlyDictionary<string, string>> result)
        {
            _result = result;
        }

        public Task<Result<IReadOnlyDictionary<string, string>>> EvaluateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class FakeMonitor : IResourceMonitor
    {
        public FakeMonitor(ResourceSnapshot snapshot)
        {
            Current = snapshot;
        }

        public ResourceSnapshot Current { get; }

        public long IncrementActiveRequests() => 0;

        public long DecrementActiveRequests() => 0;
    }
}

