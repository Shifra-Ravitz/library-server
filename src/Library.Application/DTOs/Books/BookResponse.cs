using Library.Application.DTOs.Categories;

namespace Library.Application.DTOs.Books;

public sealed class BookResponse
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public int PublicationYear { get; init; }

    public IReadOnlyList<CategoryResponse> Categories { get; init; } = [];

    /// <summary>
    /// Copies whose status is Available (Loaned and Retired copies are not counted).
    /// </summary>
    public int AvailableCopiesCount { get; init; }
}
