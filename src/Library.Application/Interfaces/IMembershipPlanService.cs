using Library.Application.DTOs.MembershipPlans;

namespace Library.Application.Interfaces;

/// <summary>
/// Read-only access to the membership plans, for GET /api/membership-plans.
/// </summary>
public interface IMembershipPlanService
{
    Task<IReadOnlyList<MembershipPlanResponse>> GetAllAsync(CancellationToken ct);
}
