using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Controllers;
using TrueRag.Api.Services;
using TrueRag.Core.Primitives;

namespace TrueRag.UnitTests.Api;

public sealed class HealthControllerTests
{
    [Fact]
    public void Live_ReturnsAlive()
    {
        var controller = new HealthController();

        var result = controller.Live();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Ready_Returns503_WhenDependencyUnavailable()
    {
        var controller = new HealthController();

        var result = await controller.Ready(new FakeReadinessEvaluator(Result<IReadOnlyDictionary<string, string>>.Failure(
            new Error("health.readiness_failed", "down", ErrorType.Unavailable))), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
    }

    [Fact]
    public async Task Ready_Returns200_WhenDependencyReady()
    {
        var controller = new HealthController();

        var result = await controller.Ready(new FakeReadinessEvaluator(Result<IReadOnlyDictionary<string, string>>.Success(
            new Dictionary<string, string> { ["storage_read"] = "ready", ["storage_write"] = "ready" })), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    private sealed class FakeReadinessEvaluator : IDependencyReadinessEvaluator
    {
        private readonly Result<IReadOnlyDictionary<string, string>> _result;

        public FakeReadinessEvaluator(Result<IReadOnlyDictionary<string, string>> result)
        {
            _result = result;
        }

        public Task<Result<IReadOnlyDictionary<string, string>>> EvaluateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }
}
