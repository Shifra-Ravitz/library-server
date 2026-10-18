using Library.Domain.Common;
using Library.Domain.Enums;

namespace Library.Domain.Entities;

/// <summary>
/// A registered user of the library (member or librarian).
/// </summary>
public class User : IHasVersion
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

    /// <summary>
    /// Number of active (not yet returned) loans, compared to <c>MembershipPlan.MaxConcurrentLoans</c>
    /// when borrowing. Updated in the same save as the loan; never negative.
    /// </summary>
    public int ActiveLoansCount { get; set; }

    /// <summary>
    /// Concurrency token. It changes with ActiveLoansCount, so two simultaneous borrows by the same user
    /// cannot both pass the quota check and both be saved.
    /// </summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
