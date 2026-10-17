namespace Library.Application.DTOs.MembershipPlans;

public sealed class MembershipPlanResponse
{
    /// <summary>
    /// Sent back by the client as RegisterRequest.MembershipPlanId.
    /// </summary>
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int MaxConcurrentLoans { get; init; }

    public decimal Price { get; init; }
}
