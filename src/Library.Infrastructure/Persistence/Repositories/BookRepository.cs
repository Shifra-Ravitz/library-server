using Library.Application.Common;
using Library.Application.Interfaces;
using Library.Domain.Entities;
using Library.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public sealed class BookRepository : IBookRepository
{
    private readonly AppDbContext _db;

    public BookRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<Book> Items, int TotalCount)> SearchAsync(BookSearchFilter filter, CancellationToken ct)
    {
        // The only place that filters IsDeleted (there is no global query filter).
        var query = _db.Books
            .AsNoTracking()
            .Where(b => !b.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Title))
        {
            query = query.Where(b => b.Title.Contains(filter.Title));
        }

        if (!string.IsNullOrWhiteSpace(filter.Author))
        {
            query = query.Where(b => b.Author.Contains(filter.Author));
        }

        if (filter.CategoryId is int categoryId)
        {
            query = query.Where(b => b.Categories.Any(c => c.Id == categoryId));
        }

        if (filter.HasAvailableCopy is bool hasAvailable)
        {
            query = hasAvailable
                ? query.Where(b => b.Copies.Any(c => c.Status == CopyStatus.Available))
                : query.Where(b => !b.Copies.Any(c => c.Status == CopyStatus.Available));
        }

        // Counted in the database, before paging.
        var totalCount = await query.CountAsync(ct);

        // ThenBy(Id) makes the order stable, so pages never overlap or skip rows.
        var ordered = (filter.SortBy, filter.Descending) switch
        {
            (BookSortBy.Author, false) => query.OrderBy(b => b.Author).ThenBy(b => b.Id),
            (BookSortBy.Author, true) => query.OrderByDescending(b => b.Author).ThenBy(b => b.Id),
            (BookSortBy.Year, false) => query.OrderBy(b => b.PublicationYear).ThenBy(b => b.Id),
            (BookSortBy.Year, true) => query.OrderByDescending(b => b.PublicationYear).ThenBy(b => b.Id),
            (_, false) => query.OrderBy(b => b.Title).ThenBy(b => b.Id),
            (_, true) => query.OrderByDescending(b => b.Title).ThenBy(b => b.Id),
        };

        var items = await ordered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Include(b => b.Categories)
            .Include(b => b.Copies.Where(c => c.Status != CopyStatus.Retired))
            .AsSplitQuery()
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Book?> GetByIdAsync(int id, CancellationToken ct)
    {
        // IsDeleted is not filtered here; the service checks it.
        return await _db.Books
            .AsNoTracking()
            .Include(b => b.Categories)
            .Include(b => b.Copies.Where(c => c.Status != CopyStatus.Retired))
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Book?> GetForUpdateAsync(int id, CancellationToken ct)
    {
        return await _db.Books
            .Include(b => b.Categories)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Book?> GetWithCopiesForUpdateAsync(int id, CancellationToken ct)
    {
        return await _db.Books
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public void Add(Book book) => _db.Books.Add(book);
}
