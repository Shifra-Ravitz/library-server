using Library.Application.Common;
using Library.Domain.Entities;

namespace Library.Application.Interfaces;

/// <summary>
/// Data access for <see cref="Book"/>. Only <see cref="SearchAsync"/> hides deleted books;
/// the other methods return the book as stored, and the service checks <c>IsDeleted</c>.
/// </summary>
public interface IBookRepository
{
    /// <summary>
    /// A page of non-deleted books matching the filter, with Categories and non-Retired Copies,
    /// plus the total number of matches before paging.
    /// </summary>
    Task<(IReadOnlyList<Book> Items, int TotalCount)> SearchAsync(BookSearchFilter filter, CancellationToken ct);

    /// <summary>
    /// Read-only. Includes Categories and non-Retired Copies.
    /// </summary>
    Task<Book?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Tracked. No navigations; used when only the book row itself is needed (e.g. adding a copy).
    /// </summary>
    Task<Book?> GetForUpdateAsync(int id, CancellationToken ct);

    /// <summary>
    /// Tracked. Includes Categories, so an update can remove dropped categories and skip existing ones.
    /// </summary>
    Task<Book?> GetWithCategoriesForUpdateAsync(int id, CancellationToken ct);

    /// <summary>
    /// Tracked. Includes all Copies (also Retired), for the soft delete.
    /// </summary>
    Task<Book?> GetWithCopiesForUpdateAsync(int id, CancellationToken ct);

    void Add(Book book);
}
