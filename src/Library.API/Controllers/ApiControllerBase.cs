using System.Globalization;
using System.Security.Claims;
using Library.API.Filters;
using Library.Application.Common;
using Library.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

/// <summary>
/// Base for all API controllers. The only place that translates an <see cref="Error"/> to an HTTP status code.
/// Every endpoint requires a valid token unless it is explicitly marked [AllowAnonymous].
/// </summary>
[ApiController]
[Authorize]
[ProducesJson]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// The id of the signed-in user, from the NameIdentifier claim written by JwtTokenGenerator.
    /// Only valid on endpoints that require authentication; a missing or invalid claim is a server bug, not a client error.
    /// </summary>
    protected int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidOperationException("The NameIdentifier claim is missing or is not a valid user id.");

    protected bool IsLibrarian => User.IsInRole(nameof(UserRole.Librarian));
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
