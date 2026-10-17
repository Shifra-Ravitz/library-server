using Library.Application.Interfaces;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _db.Users
            .AsNoTracking()
            .Include(u => u.MembershipPlan)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct)
    {
        return await _db.Users.AnyAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetWithPlanForUpdateAsync(int id, CancellationToken ct)
    {
        return await _db.Users
            .Include(u => u.MembershipPlan)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public void Add(User user) => _db.Users.Add(user);
}
