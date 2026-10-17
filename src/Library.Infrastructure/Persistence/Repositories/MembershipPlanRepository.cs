using Library.Application.Interfaces;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public sealed class MembershipPlanRepository : IMembershipPlanRepository
{
    private readonly AppDbContext _db;

    public MembershipPlanRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MembershipPlan>> GetAllAsync(CancellationToken ct)
    {
        return await _db.MembershipPlans
            .AsNoTracking()
            .OrderBy(p => p.MaxConcurrentLoans)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken ct)
    {
        return await _db.MembershipPlans.AnyAsync(p => p.Id == id, ct);
    }
}
