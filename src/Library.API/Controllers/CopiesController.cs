using Library.Application.DTOs.Copies;
using Library.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

/// <summary>
/// Copies are listed and created under their book, and read or retired by their own id.
/// </summary>
[Route("api")]
public sealed class CopiesController : ApiControllerBase
{
    private readonly IBookCopyService _copies;

    public CopiesController(IBookCopyService copies)
    {
        _copies = copies;
    }

    [HttpGet("books/{bookId:int}/copies")]
    [ProducesResponseType(typeof(IReadOnlyList<BookCopyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BookCopyResponse>>> GetByBook([FromRoute] int bookId, CancellationToken ct)
    {
        var result = await _copies.GetByBookIdAsync(bookId, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpPost("books/{bookId:int}/copies")]
    [ProducesResponseType(typeof(BookCopyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookCopyResponse>> Create([FromRoute] int bookId, CancellationToken ct)
    {
        var result = await _copies.CreateAsync(bookId, ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("copies/{id:int}")]
    [ProducesResponseType(typeof(BookCopyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookCopyResponse>> GetById([FromRoute] int id, CancellationToken ct)
    {
        var result = await _copies.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpDelete("copies/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        return FromResult(await _copies.DeleteAsync(id, ct));
    }
}
