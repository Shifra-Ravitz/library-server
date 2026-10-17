using Library.Domain.Entities;

namespace Library.Application.Interfaces;

/// <summary>
/// Data access for <see cref="User"/>.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Read-only. Includes MembershipPlan.
    /// </summary>
    Task<User?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Read-only.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, CancellationToken ct);

    /// <summary>
    /// Tracked. Includes MembershipPlan, for the loan quota check.
    /// </summary>
    Task<User?> GetWithPlanForUpdateAsync(int id, CancellationToken ct);

    void Add(User user);
}
