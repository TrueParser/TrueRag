using TrueRag.Core.Primitives;

namespace TrueRag.UnitTests.Core;

public sealed class ResultTests
{
    [Fact]
    public void SuccessResult_HasExpectedFlags()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void FailureResult_HasExpectedFlags()
    {
        var result = Result.Failure(new Error("code", "message", ErrorType.Validation));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void GenericResult_SuccessCarriesValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }
}