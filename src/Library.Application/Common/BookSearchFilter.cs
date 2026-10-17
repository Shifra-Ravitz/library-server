namespace Library.Application.Common;

/// <summary>
/// Search criteria passed from the book service to <see cref="Interfaces.IBookRepository.SearchAsync"/>.
/// Input validation happens on the API DTO; this record only carries already-validated values.
/// </summary>
public sealed record BookSearchFilter
{
    public string? Title { get; init; }

    public string? Author { get; init; }

    public int? CategoryId { get; init; }

    /// <summary>
    /// true: only books with at least one Available copy; false: only books without one; null: no filter.
    /// </summary>
    public bool? HasAvailableCopy { get; init; }

    public BookSortBy SortBy { get; init; } = BookSortBy.Title;

    public bool Descending { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
