namespace Library.Application.Common;

/// <summary>
/// Search criteria passed from the loan service to <see cref="Interfaces.ILoanRepository.SearchAsync"/>.
/// </summary>
public sealed record LoanSearchFilter
{
    /// <summary>
    /// Loans of this user only; null returns the loans of all users.
    /// </summary>
    public int? UserId { get; init; }

    /// <summary>
    /// Only loans that were not returned yet.
    /// </summary>
    public bool ActiveOnly { get; init; }

    /// <summary>
    /// Only loans that were not returned and whose due date is before <see cref="NowUtc"/>.
    /// </summary>
    public bool OverdueOnly { get; init; }

    /// <summary>
    /// Only loans that were already returned.
    /// </summary>
    public bool ReturnedOnly { get; init; }

    /// <summary>
    /// The current time (UTC), supplied by the service so the query is deterministic and testable.
    /// </summary>
    public DateTime NowUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
