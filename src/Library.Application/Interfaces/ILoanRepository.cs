using Library.Application.Common;
using Library.Domain.Entities;

namespace Library.Application.Interfaces;

/// <summary>
/// Data access for <see cref="Loan"/>.
/// </summary>
public interface ILoanRepository
{
    /// <summary>
    /// Read-only. Includes BookCopy with its Book, and User.
    /// </summary>
    Task<Loan?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Tracked. Includes BookCopy, so returning the loan can also free the copy.
    /// </summary>
    Task<Loan?> GetForUpdateAsync(int id, CancellationToken ct);

    /// <summary>
    /// Whether the user has a loan that was not returned and whose due date is before <paramref name="nowUtc"/>.
    /// </summary>
    Task<bool> HasOverdueLoanAsync(int userId, DateTime nowUtc, CancellationToken ct);

    /// <summary>
    /// A page of loans matching the filter, newest first, plus the total number of matches before paging.
    /// </summary>
    Task<(IReadOnlyList<Loan> Items, int TotalCount)> SearchAsync(LoanSearchFilter filter, CancellationToken ct);

    void Add(Loan loan);
}
