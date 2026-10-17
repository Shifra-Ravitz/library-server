using Library.Domain.Entities;

namespace Library.Application.Interfaces;

/// <summary>
/// Data access for <see cref="Category"/>. Categories are deleted physically.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// Read-only, ordered by Name.
    /// </summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Read-only.
    /// </summary>
    Task<Category?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Tracked.
    /// </summary>
    Task<Category?> GetForUpdateAsync(int id, CancellationToken ct);

    /// <summary>
    /// Tracked. Used to attach categories to a book. Ids that do not exist are simply missing from the result.
    /// </summary>
    Task<IReadOnlyList<Category>> GetByIdsForUpdateAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    /// <summary>
    /// Whether another category already has this name. <paramref name="excludeId"/> skips the category being updated.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct);

    void Add(Category category);

    void Remove(Category category);
}
