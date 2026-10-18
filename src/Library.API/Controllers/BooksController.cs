using Library.Application.Common;
using Library.Application.DTOs.Books;
using Library.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[Route("api/books")]
public sealed class BooksController : ApiControllerBase
{
    private readonly IBookService _books;

    public BooksController(IBookService books)
    {
        _books = books;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BookResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<BookResponse>>> Search([FromQuery] BookQueryParameters query, CancellationToken ct)
    {
        return Ok(await _books.SearchAsync(query, ct));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> GetById([FromRoute] int id, CancellationToken ct)
    {
        var result = await _books.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BookResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookResponse>> Create([FromBody] CreateBookRequest request, CancellationToken ct)
    {
        var result = await _books.CreateAsync(request, ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update([FromRoute] int id, [FromBody] UpdateBookRequest request, CancellationToken ct)
    {
        return FromResult(await _books.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        return FromResult(await _books.DeleteAsync(id, ct));
    }
}
