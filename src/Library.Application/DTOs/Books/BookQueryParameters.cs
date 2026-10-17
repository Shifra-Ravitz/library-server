using System.ComponentModel.DataAnnotations;
using Library.Application.Common;

namespace Library.Application.DTOs.Books;

/// <summary>
/// Query string of GET /api/books. The service converts it to <see cref="BookSearchFilter"/>.
/// </summary>
public sealed class BookQueryParameters
{
    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(150)]
    public string? Author { get; set; }

    [Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }

    public bool? HasAvailableCopy { get; set; }

    /// <summary>
    /// Rejects numeric values that are not defined in <see cref="BookSortBy"/> (e.g. sortBy=7).
    /// </summary>
    [EnumDataType(typeof(BookSortBy))]
    public BookSortBy SortBy { get; set; } = BookSortBy.Title;

    public bool Descending { get; set; }

    [Range(1, Paging.MaxPage)]
    public int Page { get; set; } = 1;

    [Range(1, Paging.MaxPageSize)]
    public int PageSize { get; set; } = Paging.DefaultPageSize;
}
