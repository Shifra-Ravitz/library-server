using Library.Application.Interfaces;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    public CategoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct)
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Category?> GetForUpdateAsync(int id, CancellationToken ct)
    {
        return await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<Category>> GetByIdsForUpdateAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        // One query (WHERE "Id" = ANY(@ids)), not a query per id.
        return await _db.Categories
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct);
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct)
    {
        var query = _db.Categories.Where(c => c.Name == name);

        if (excludeId is int id)
        {
            query = query.Where(c => c.Id != id);
        }

        return await query.AnyAsync(ct);
    }

    public void Add(Category category) => _db.Categories.Add(category);

    // Physical delete; the BookCategories join rows are removed by cascade.
    public void Remove(Category category) => _db.Categories.Remove(category);
}
