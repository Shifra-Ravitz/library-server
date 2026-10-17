using Library.Domain.Entities;

namespace Library.Application.Interfaces;

/// <summary>
/// Read-only data access for <see cref="MembershipPlan"/>.
/// </summary>
public interface IMembershipPlanRepository
{
    /// <summary>
    /// Ordered by MaxConcurrentLoans.
    /// </summary>
    Task<IReadOnlyList<MembershipPlan>> GetAllAsync(CancellationToken ct);

    Task<bool> ExistsAsync(int id, CancellationToken ct);
}
