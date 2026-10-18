using System.Globalization;
using AutoMapper;
using Library.Application.Common;
using Library.Application.Common.Exceptions;
using Library.Application.DTOs.Loans;
using Library.Application.Interfaces;
using Library.Application.Options;
using Library.Domain.Entities;
using Library.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Application.Services;

public sealed class LoanService : ILoanService
{
    private const string BorrowConflictMessage =
        "העותק נתפס על ידי משתמשת אחרת, או שנשלחה במקביל בקשת השאלה נוספת מהחשבון שלך. נסי שוב.";
    private const string ReturnConflictMessage = "ההשאלה עודכנה בינתיים על ידי בקשה אחרת. נסי שוב.";
    private const string QuotaReachedMessageFormat =
        "הגעת למכסת ההשאלות של המנוי שלך (עד {0} ספרים בו-זמנית). החזירי ספר כדי לשאול ספר נוסף.";

    private readonly ILoanRepository _loans;
    private readonly IBookCopyRepository _copies;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly LoanOptions _options;
    private readonly ILogger<LoanService> _logger;

    public LoanService(
        ILoanRepository loans,
        IBookCopyRepository copies,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IOptions<LoanOptions> options,
        ILogger<LoanService> logger)
    {
        _loans = loans;
        _copies = copies;
        _users = users;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<LoanResponse>> BorrowAsync(int userId, int copyId, CancellationToken ct)
    {
        // Tracked, because the copy's Status changes below. Includes Book for the IsDeleted check.
        var copy = await _copies.GetForUpdateAsync(copyId, ct);

        // The client can send any copyId, so a copy of a deleted book must be rejected here explicitly.
        if (copy is null || copy.Status == CopyStatus.Retired || copy.Book.IsDeleted)
        {
            return Error.NotFound("העותק לא נמצא.");
        }

        if (copy.Status != CopyStatus.Available)
        {
            return Error.Conflict("העותק כבר מושאל.");
        }

        // Read once, so the overdue check, LoanedAtUtc and DueAtUtc all use the same moment.
        var nowUtc = DateTime.UtcNow;

        if (await _loans.HasOverdueLoanAsync(userId, nowUtc, ct))
        {
            return Error.Conflict("יש לך ספר שמועד ההחזרה שלו עבר. יש להחזיר אותו לפני השאלה נוספת.");
        }

        // Tracked, because ActiveLoansCount changes below. Two simultaneous borrows by the same user can both
        // pass the quota check, but the user's Version lets only one of them save.
        var user = await _users.GetForUpdateAsync(userId, ct);
        if (user is null)
        {
            // The token is valid, but its user no longer exists.
            return Error.Unauthorized("המשתמשת לא נמצאה. יש להתחבר מחדש.");
        }

        if (user.MembershipPlan is null)
        {
            return Error.Forbidden("אין לך מנוי, ולכן אי אפשר לשאול ספרים.");
        }

        if (user.ActiveLoansCount >= user.MembershipPlan.MaxConcurrentLoans)
        {
            return Error.Conflict(string.Format(
                CultureInfo.InvariantCulture, QuotaReachedMessageFormat, user.MembershipPlan.MaxConcurrentLoans));
        }

        var loan = new Loan
        {
            UserId = userId,
            BookCopy = copy,
            LoanedAtUtc = nowUtc,
            DueAtUtc = nowUtc.AddDays(_options.LoanPeriodDays),
        };

        copy.Status = CopyStatus.Loaned;
        user.ActiveLoansCount++;
        _loans.Add(loan);

        // One save: the new loan, the copy's status change and the user's counter are committed together.
        // If another request changed the copy or the user since they were read, that UPDATE matches no row
        // and everything else is rolled back with it.
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // An expected race, not a bug: no exception in the log, and no second save on this context.
            _logger.LogWarning("Concurrency conflict while borrowing copy {CopyId} by user {UserId}", copyId, userId);
            return Error.Conflict(BorrowConflictMessage);
        }

        // Reloaded for display, because the tracked loan has no User (needed for UserName).
        var created = await _loans.GetByIdAsync(loan.Id, ct)
            ?? throw new InvalidOperationException($"Loan {loan.Id} was not found right after it was saved.");

        return _mapper.Map<LoanResponse>(created);
    }

    public async Task<Result> ReturnAsync(int userId, bool isLibrarian, int loanId, CancellationToken ct)
    {
        // Tracked, with BookCopy, so returning the loan can also free the copy.
        var loan = await _loans.GetForUpdateAsync(loanId, ct);
        if (loan is null)
        {
            return LoanNotFound();
        }

        if (!isLibrarian && loan.UserId != userId)
        {
            return Error.Forbidden("אין לך הרשאה להחזיר השאלה של משתמשת אחרת.");
        }

        if (loan.ReturnedAtUtc is not null)
        {
            return Error.Conflict("ההשאלה כבר הוחזרה.");
        }

        // The loan's owner, not the caller: a librarian may return a loan on behalf of a member.
        var owner = await _users.GetForUpdateAsync(loan.UserId, ct)
            ?? throw new InvalidOperationException($"User {loan.UserId} of loan {loan.Id} was not found.");

        loan.ReturnedAtUtc = DateTime.UtcNow;
        loan.BookCopy.Status = CopyStatus.Available;

        if (owner.ActiveLoansCount > 0)
        {
            owner.ActiveLoansCount--;
        }
        else
        {
            // Not expected, except for a loan that was already active when the counter was added.
            // The counter is not decremented below zero (the database would reject it anyway).
            _logger.LogWarning(
                "ActiveLoansCount is already 0 while returning loan {LoanId} of user {UserId}; not decremented",
                loan.Id, loan.UserId);
        }

        // Loan has no Version, but the copy and the user do: a second return of the same loan fails on their UPDATE.
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Concurrency conflict while returning loan {LoanId} (copy {CopyId}) by user {UserId}",
                loanId, loan.BookCopyId, userId);
            return Error.Conflict(ReturnConflictMessage);
        }

        return Result.Success();
    }

    public async Task<Result<LoanResponse>> GetByIdAsync(int userId, bool isLibrarian, int loanId, CancellationToken ct)
    {
        var loan = await _loans.GetByIdAsync(loanId, ct);
        if (loan is null)
        {
            return LoanNotFound();
        }

        if (!isLibrarian && loan.UserId != userId)
        {
            return Error.Forbidden("אין לך הרשאה לצפות בהשאלה הזאת.");
        }

        return _mapper.Map<LoanResponse>(loan);
    }

    public Task<PagedResult<LoanResponse>> GetAllAsync(LoanQueryParameters query, CancellationToken ct) =>
        SearchAsync(null, query, ct);

    public Task<PagedResult<LoanResponse>> GetForUserAsync(int userId, LoanQueryParameters query, CancellationToken ct) =>
        SearchAsync(userId, query, ct);

    /// <summary>
    /// Shared by both list operations; <paramref name="userId"/> null means all users.
    /// </summary>
    private async Task<PagedResult<LoanResponse>> SearchAsync(int? userId, LoanQueryParameters query, CancellationToken ct)
    {
        // Built by hand: the filter is an internal contract with the repository, not a DTO.
        var filter = new LoanSearchFilter
        {
            UserId = userId,
            ActiveOnly = query.ActiveOnly,
            OverdueOnly = query.OverdueOnly,
            ReturnedOnly = query.ReturnedOnly,
            NowUtc = DateTime.UtcNow,
            Page = query.Page,
            PageSize = query.PageSize,
        };

        var (items, totalCount) = await _loans.SearchAsync(filter, ct);
        var responses = _mapper.Map<List<LoanResponse>>(items);

        return new PagedResult<LoanResponse>(responses, filter.Page, filter.PageSize, totalCount);
    }

    private static Error LoanNotFound() => Error.NotFound("ההשאלה לא נמצאה.");
}
