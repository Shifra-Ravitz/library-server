using Library.Application.Common;
using Library.Application.DTOs.Loans;

namespace Library.Application.Interfaces;

/// <summary>
/// Borrowing and returning copies. The user id and role always come from the caller's token, never from the request.
/// </summary>
public interface ILoanService
{
    /// <summary>
    /// Lends an available copy to the user, unless the user has an overdue loan.
    /// </summary>
    Task<Result<LoanResponse>> BorrowAsync(int userId, int copyId, CancellationToken ct);

    /// <summary>
    /// Returns the loan and frees its copy. Allowed for the borrower or a librarian.
    /// </summary>
    Task<Result> ReturnAsync(int userId, bool isLibrarian, int loanId, CancellationToken ct);

    /// <summary>
    /// A single loan. Visible to the borrower or a librarian.
    /// </summary>
    Task<Result<LoanResponse>> GetByIdAsync(int userId, bool isLibrarian, int loanId, CancellationToken ct);

    /// <summary>
    /// The loans of all users (librarian view).
    /// </summary>
    Task<PagedResult<LoanResponse>> GetAllAsync(LoanQueryParameters query, CancellationToken ct);

    /// <summary>
    /// The loans of one user.
    /// </summary>
    Task<PagedResult<LoanResponse>> GetForUserAsync(int userId, LoanQueryParameters query, CancellationToken ct);
}
