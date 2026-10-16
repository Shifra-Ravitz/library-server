namespace Library.Domain.Entities;

/// <summary>
/// A membership tier that determines how many books a user may borrow at the same time.
/// </summary>
public class MembershipPlan
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of active (not yet returned) loans allowed for users on this plan.
    /// </summary>
    public int MaxConcurrentLoans { get; set; }

    /// <summary>
    /// Plan price. Informational only; no billing logic depends on it.
    /// </summary>
    public decimal Price { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
