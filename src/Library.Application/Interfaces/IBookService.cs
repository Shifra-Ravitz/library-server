using Library.Application.Common;
using Library.Application.DTOs.Books;

namespace Library.Application.Interfaces;

/// <summary>
/// Book catalog operations. Deleted books behave as if they do not exist.
/// </summary>
public interface IBookService
{
    Task<PagedResult<BookResponse>> SearchAsync(BookQueryParameters query, CancellationToken ct);

    Task<Result<BookResponse>> GetByIdAsync(int id, CancellationToken ct);

    Task<Result<BookResponse>> CreateAsync(CreateBookRequest request, CancellationToken ct);

    Task<Result> UpdateAsync(int id, UpdateBookRequest request, CancellationToken ct);

    /// <summary>
    /// Soft delete: marks the book deleted and retires its copies.
    /// </summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}
