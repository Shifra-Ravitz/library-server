namespace Library.Application.DTOs.Users;

/// <summary>
/// Public view of a user. PasswordHash is never exposed.
/// </summary>
public sealed class UserResponse
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Null for librarians, who have no plan.
    /// </summary>
    public int? MembershipPlanId { get; init; }

    public string? MembershipPlanName { get; init; }
}
