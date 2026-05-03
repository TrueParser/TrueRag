using TrueRag.Api.Services;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Primitives;

namespace TrueRag.UnitTests.Api;

public sealed class DependencyReadinessEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_ReturnsSuccess_WhenReadAndWriteHealthy()
    {
        var evaluator = new DependencyReadinessEvaluator(new FakeStorageProbe(Result.Success(), Result.Success()));

        var result = await evaluator.EvaluateAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("ready", result.Value!["storage_read"]);
        Assert.Equal("ready", result.Value!["storage_write"]);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsUnavailable_WhenAnyDependencyFails()
    {
        var evaluator = new DependencyReadinessEvaluator(new FakeStorageProbe(Result.Success(), Result.Failure(new Error("db.down", "down", ErrorType.Unavailable))));

        var result = await evaluator.EvaluateAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unavailable, result.Error!.Type);
    }

    private sealed class FakeStorageProbe : IStorageHealthProbe
    {
        private readonly Result _read;
        private readonly Result _write;

        public FakeStorageProbe(Result read, Result write)
        {
            _read = read;
            _write = write;
        }

        public Task<Result> CheckReadStoreAsync(CancellationToken cancellationToken = default) => Task.FromResult(_read);

        public Task<Result> CheckWriteStoreAsync(CancellationToken cancellationToken = default) => Task.FromResult(_write);
    }
}
