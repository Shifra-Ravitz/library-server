using Library.Domain.Common;
using Library.Domain.Enums;

namespace Library.Domain.Entities;

/// <summary>
/// A single physical copy of a <see cref="Book"/>. This is the unit that is actually loaned,
/// and it is protected by optimistic concurrency to prevent two users borrowing the same copy.
/// </summary>
public class BookCopy : IHasVersion
{
    public int Id { get; set; }

    public CopyStatus Status { get; set; } = CopyStatus.Available;

    /// <inheritdoc />
    public Guid Version { get; set; } = Guid.NewGuid();

    public int BookId { get; set; }

    public Book Book { get; set; } = null!;

    /// <summary>
    /// Loan history of this copy (at most one of them is active at any time).
    /// </summary>
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
