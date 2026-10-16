namespace Library.Domain.Entities;

/// <summary>
/// A book category (genre/subject). Has a many-to-many relationship with <see cref="Book"/>
/// without an explicit join entity.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Book> Books { get; set; } = new List<Book>();
}
