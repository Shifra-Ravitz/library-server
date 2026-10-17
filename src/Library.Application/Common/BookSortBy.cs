namespace Library.Application.Common;

/// <summary>
/// Sort field for the book search. <see cref="Year"/> sorts by <c>Book.PublicationYear</c>.
/// </summary>
public enum BookSortBy
{
    Title = 0,
    Author = 1,
    Year = 2
}
