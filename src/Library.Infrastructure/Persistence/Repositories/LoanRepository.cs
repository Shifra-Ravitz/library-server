using Library.Application.Common;
using Library.Application.Interfaces;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public sealed class LoanRepository : ILoanRepository
{
    private readonly AppDbContext _db;

    public LoanRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Loan?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _db.Loans
            .AsNoTracking()
            .Include(l => l.BookCopy)
                .ThenInclude(c => c.Book)
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task<Loan?> GetForUpdateAsync(int id, CancellationToken ct)
    {
        return await _db.Loans
            .Include(l => l.BookCopy)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task<bool> HasOverdueLoanAsync(int userId, DateTime nowUtc, CancellationToken ct)
    {
        return await _db.Loans.AnyAsync(
            l => l.UserId == userId && l.ReturnedAtUtc == null && l.DueAtUtc < nowUtc,
            ct);
    }

    public async Task<(IReadOnlyList<Loan> Items, int TotalCount)> SearchAsync(LoanSearchFilter filter, CancellationToken ct)
    {
        var query = _db.Loans.AsNoTracking();

        if (filter.UserId is int userId)
        {
            query = query.Where(l => l.UserId == userId);
        }

        if (filter.ActiveOnly)
        {
            query = query.Where(l => l.ReturnedAtUtc == null);
        }

        if (filter.OverdueOnly)
        {
            query = query.Where(l => l.ReturnedAtUtc == null && l.DueAtUtc < filter.NowUtc);
        }

        var totalCount = await query.CountAsync(ct);

        // Only reference navigations are included, so a single query is fine (no split needed).
        var items = await query
            .OrderByDescending(l => l.LoanedAtUtc)
            .ThenByDescending(l => l.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Include(l => l.BookCopy)
                .ThenInclude(c => c.Book)
            .Include(l => l.User)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void Add(Loan loan) => _db.Loans.Add(loan);
}
