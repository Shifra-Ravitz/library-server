using Library.Application.Common;
using Library.Application.DTOs.Loans;
using Library.Application.Interfaces;
using Library.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

/// <summary>
/// Borrowing and returning. The user id always comes from the token (<see cref="ApiControllerBase.CurrentUserId"/>).
/// </summary>
[Route("api/loans")]
public sealed class LoansController : ApiControllerBase
{
    private readonly ILoanService _loans;

    public LoansController(ILoanService loans)
    {
        _loans = loans;
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Member))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Create([FromBody] CreateLoanRequest request, CancellationToken ct)
    {
        var result = await _loans.BorrowAsync(CurrentUserId, request.CopyId, ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanResponse>> GetById([FromRoute] int id, CancellationToken ct)
    {
        var result = await _loans.GetByIdAsync(CurrentUserId, IsLibrarian, id, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Return([FromRoute] int id, CancellationToken ct)
    {
        return FromResult(await _loans.ReturnAsync(CurrentUserId, IsLibrarian, id, ct));
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(PagedResult<LoanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetAll([FromQuery] LoanQueryParameters query, CancellationToken ct)
    {
        return Ok(await _loans.GetAllAsync(query, ct));
    }

    [HttpGet("me")]
    [Authorize(Roles = nameof(UserRole.Member))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(PagedResult<LoanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetMine([FromQuery] LoanQueryParameters query, CancellationToken ct)
    {
        return Ok(await _loans.GetForUserAsync(CurrentUserId, query, ct));
    }
}
