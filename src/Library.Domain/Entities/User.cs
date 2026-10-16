using Library.Domain.Enums;

namespace Library.Domain.Entities;

/// <summary>
/// A registered user of the library (member or librarian).
/// </summary>
/// <remarks>
/// Intentionally does NOT contain ActiveLoansCount or Version and does NOT implement
/// <see cref="Common.IHasVersion"/> yet. Those are added in step 13 together with a separate migration.
/// </remarks>
public class User
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password. The plain-text password is never stored.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Member;

    public int? MembershipPlanId { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
