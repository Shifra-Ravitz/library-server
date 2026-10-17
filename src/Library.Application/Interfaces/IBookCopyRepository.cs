using Library.Domain.Entities;

namespace Library.Application.Interfaces;

/// <summary>
/// Data access for <see cref="BookCopy"/>.
/// </summary>
public interface IBookCopyRepository
{
    /// <summary>
    /// Read-only. Includes Book.
    /// </summary>
    Task<BookCopy?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Read-only. The non-Retired copies of a book, ordered by Id. Includes Book.
    /// </summary>
    Task<IReadOnlyList<BookCopy>> GetByBookIdAsync(int bookId, CancellationToken ct);

    /// <summary>
    /// Tracked. Includes Book, so the service can check <c>copy.Book.IsDeleted</c>.
    /// </summary>
    Task<BookCopy?> GetForUpdateAsync(int id, CancellationToken ct);

    void Add(BookCopy copy);
}
