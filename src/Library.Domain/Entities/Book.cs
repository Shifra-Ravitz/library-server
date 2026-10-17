namespace Library.Domain.Entities;

/// <summary>
/// A book title (catalog entry). The physical items that can be borrowed are its <see cref="Copies"/>.
/// </summary>
public class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public int PublicationYear { get; set; }

    /// <summary>
    /// A logically deleted book; its copies are Retired and cannot be borrowed.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Categories of this book (many-to-many, no explicit join entity).
    /// </summary>
    public ICollection<Category> Categories { get; set; } = new List<Category>();

    /// <summary>
    /// Physical copies of this book that can be loaned.
    /// </summary>
    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();
}
