using Library.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

/// <summary>
/// Base for all API controllers. The only place that translates an <see cref="Error"/> to an HTTP status code.
/// Every endpoint requires a valid token unless it is explicitly marked [AllowAnonymous].
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Returns an RFC 7807 problem response whose status matches the error type.
    /// </summary>
    protected ActionResult ToProblem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Type, "Unhandled error type."),
        };

        return Problem(detail: error.Message, statusCode: statusCode);
    }

    /// <summary>
    /// For operations without a value (PUT, DELETE): 204 on success, otherwise a problem response.
    /// </summary>
    protected ActionResult FromResult(Result result) =>
        result.IsSuccess ? NoContent() : ToProblem(result.Error);
}
