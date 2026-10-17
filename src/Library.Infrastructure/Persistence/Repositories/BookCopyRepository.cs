using Library.Application.Interfaces;
using Library.Domain.Entities;
using Library.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public sealed class BookCopyRepository : IBookCopyRepository
{
    private readonly AppDbContext _db;

    public BookCopyRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BookCopy?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _db.BookCopies
            .AsNoTracking()
            .Include(c => c.Book)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<BookCopy>> GetByBookIdAsync(int bookId, CancellationToken ct)
    {
        return await _db.BookCopies
            .AsNoTracking()
            .Include(c => c.Book)
            .Where(c => c.BookId == bookId && c.Status != CopyStatus.Retired)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<BookCopy?> GetForUpdateAsync(int id, CancellationToken ct)
    {
        // Tracked: the loan flow changes Status, and the original Version goes into the UPDATE's WHERE.
        return await _db.BookCopies
            .Include(c => c.Book)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public void Add(BookCopy copy) => _db.BookCopies.Add(copy);
}
