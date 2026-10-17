namespace Library.Application.DTOs.Copies;

public sealed class BookCopyResponse
{
    public int Id { get; init; }

    public int BookId { get; init; }

    /// <summary>
    /// Flattened from Book.Title. Every IBookCopyRepository read includes Book; null only if it was not loaded.
    /// </summary>
    public string? BookTitle { get; init; }

    public string Status { get; init; } = string.Empty;
}
