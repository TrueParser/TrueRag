using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrueRag.Api.Helpers;
using TrueRag.Core.Primitives;

namespace TrueRag.UnitTests.Api;

public sealed class ApiResultMapperTests
{
    [Fact]
    public void FromError_MapsUnavailableTo503()
    {
        var controller = new TestController();

        var result = controller.Map(new Error("x", "y", ErrorType.Unavailable));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    private sealed class TestController : ControllerBase
    {
        public IActionResult Map(Error error) => this.FromError(error);
    }
}
