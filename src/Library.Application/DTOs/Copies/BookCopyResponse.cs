namespace Library.Application.DTOs.Copies;

public sealed class BookCopyResponse
{
    public int Id { get; init; }

    public int BookId { get; init; }

    /// <summary>
    /// Null when Book was not loaded (e.g. IBookCopyRepository.GetByBookIdAsync).
    /// </summary>
    public string? BookTitle { get; init; }

    public string Status { get; init; } = string.Empty;
}
