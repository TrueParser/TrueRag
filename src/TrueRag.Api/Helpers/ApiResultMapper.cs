using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Helpers;

public static class ApiResultMapper
{
    public static IActionResult ToActionResult(this ControllerBase controller, Result result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok();
        }

        return controller.FromError(result.Error!);
    }

    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return controller.FromError(result.Error!);
    }

    public static IActionResult FromError(this ControllerBase controller, Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation => controller.BadRequest(error),
            ErrorType.NotFound => controller.NotFound(error),
            ErrorType.Conflict => controller.Conflict(error),
            ErrorType.Forbidden => controller.Forbid(),
            ErrorType.Unauthorized => controller.Unauthorized(error),
            ErrorType.Unavailable => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, error),
            ErrorType.Unexpected => controller.StatusCode(StatusCodes.Status500InternalServerError, error),
            _ => controller.BadRequest(error)
        };
    }
}
