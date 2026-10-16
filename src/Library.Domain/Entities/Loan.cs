namespace Library.Domain.Entities;

/// <summary>
/// A loan of a specific <see cref="BookCopy"/> to a <see cref="User"/>. All dates are in UTC.
/// </summary>
public class Loan
{
    public int Id { get; set; }

    /// <summary>
    /// When the copy was borrowed (UTC).
    /// </summary>
    public DateTime LoanedAtUtc { get; set; }

    /// <summary>
    /// When the copy is due to be returned (UTC).
    /// </summary>
    public DateTime DueAtUtc { get; set; }

    /// <summary>
    /// When the copy was returned (UTC); null while the loan is still active.
    /// </summary>
    public DateTime? ReturnedAtUtc { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public int BookCopyId { get; set; }

    public BookCopy BookCopy { get; set; } = null!;
}
