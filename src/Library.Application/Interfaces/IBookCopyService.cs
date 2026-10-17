using Library.Application.Common;
using Library.Application.DTOs.Copies;

namespace Library.Application.Interfaces;

/// <summary>
/// Copy operations. Retired copies, and copies of deleted books, behave as if they do not exist.
/// </summary>
public interface IBookCopyService
{
    Task<Result<IReadOnlyList<BookCopyResponse>>> GetByBookIdAsync(int bookId, CancellationToken ct);

    Task<Result<BookCopyResponse>> GetByIdAsync(int id, CancellationToken ct);

    Task<Result<BookCopyResponse>> CreateAsync(int bookId, CancellationToken ct);

    /// <summary>
    /// Retires the copy (no physical delete).
    /// </summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}
