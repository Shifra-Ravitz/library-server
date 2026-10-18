using AutoMapper;
using Library.Application.Common;
using Library.Application.Common.Exceptions;
using Library.Application.Interfaces;
using Library.Application.Options;
using Library.Application.Services;
using Library.Domain.Entities;
using Library.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Library.Tests.Services;

public class LoanServiceTests
{
    private const int MemberId = 1;
    private const int OtherMemberId = 2;
    private const int LibrarianId = 99;
    private const int BookId = 10;
    private const int CopyId = 100;
    private const int LoanId = 500;
    private const int LoanPeriodDays = 14;

    private readonly Mock<ILoanRepository> _loans = new();
    private readonly Mock<IBookCopyRepository> _copies = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Book _book;
    private readonly BookCopy _copy;
    private readonly User _member;

    // The loan passed to ILoanRepository.Add, captured by a Callback.
    private Loan? _addedLoan;

    private readonly LoanService _sut;

    // הבנאי בונה מצב "הכול תקין", וכל בדיקה משנה רק את הדבר שהיא בודקת.
    // זה עובד כי xUnit יוצר מופע חדש של מחלקת הבדיקות לכל בדיקה: כל בדיקה מקבלת אובייקטים ו-Mocks חדשים,
    // ושינוי שבדיקה אחת עשתה (למשל עותק שהפך ל-Loaned) לא משפיע על בדיקה אחרת.
    public LoanServiceTests()
    {
        _book = new Book { Id = BookId, Title = "ספר לבדיקה", Author = "מחברת", IsDeleted = false };
        _copy = new BookCopy { Id = CopyId, BookId = BookId, Book = _book, Status = CopyStatus.Available };
        _book.Copies.Add(_copy);

        _member = new User
        {
            Id = MemberId,
            Name = "לקוחה",
            Role = UserRole.Member,
            ActiveLoansCount = 0,
            MembershipPlan = new MembershipPlan { Id = 1, Name = "בסיסי", MaxConcurrentLoans = 3 },
        };

        _copies.Setup(r => r.GetForUpdateAsync(CopyId, It.IsAny<CancellationToken>())).ReturnsAsync(_copy);
        _users.Setup(r => r.GetForUpdateAsync(MemberId, It.IsAny<CancellationToken>())).ReturnsAsync(_member);
        _loans.Setup(r => r.HasOverdueLoanAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _loans.Setup(r => r.Add(It.IsAny<Loan>())).Callback<Loan>(loan => _addedLoan = loan);

        // After saving, BorrowAsync reloads the loan for display; here it gets back the loan that was added.
        _loans.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _addedLoan);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // The real profiles from Library.Application, not a mock of IMapper.
        var mapperConfig = new MapperConfiguration(
            cfg => cfg.AddMaps(typeof(LoanService).Assembly),
            NullLoggerFactory.Instance);

        _sut = new LoanService(
            _loans.Object,
            _copies.Object,
            _users.Object,
            _unitOfWork.Object,
            mapperConfig.CreateMapper(),
            Options.Create(new LoanOptions { LoanPeriodDays = LoanPeriodDays }),
            NullLogger<LoanService>.Instance);
    }

    [Fact]
    public async Task BorrowAsync_CopyAvailableAndUnderQuota_ReturnsSuccess()
    {
        // Arrange: nothing to change, the constructor already built the valid state.

        // Act
        var result = await _sut.BorrowAsync(MemberId, CopyId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(CopyStatus.Loaned, _copy.Status);
        Assert.Equal(1, _member.ActiveLoansCount);

        Assert.NotNull(_addedLoan);
        Assert.Equal(MemberId, _addedLoan.UserId);
        // The service sets the BookCopy navigation, not BookCopyId; EF fills the FK only when saving.
        Assert.Same(_copy, _addedLoan.BookCopy);
        Assert.Equal(CopyId, _addedLoan.BookCopy.Id);
        Assert.Null(_addedLoan.ReturnedAtUtc);
        Assert.InRange((_addedLoan.DueAtUtc - _addedLoan.LoanedAtUtc).TotalDays, 13.99, 14.01);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BorrowAsync_CopyNotFound_ReturnsNotFound()
    {
        // Arrange
        _copies.Setup(r => r.GetForUpdateAsync(CopyId, It.IsAny<CancellationToken>())).ReturnsAsync((BookCopy?)null);

        // Act
        var result = await _sut.BorrowAsync(MemberId, CopyId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.NotFound);
        VerifyNotSaved();
    }

    [Fact]
    public async Task BorrowAsync_CopyAlreadyLoaned_ReturnsConflict()
    {
        // Arrange
        _copy.Status = CopyStatus.Loaned;

        // Act
        var result = await _sut.BorrowAsync(MemberId, CopyId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.Conflict);
        Assert.Equal(0, _member.ActiveLoansCount);
        VerifyNotSaved();
    }

    [Fact]
    public async Task BorrowAsync_QuotaFull_ReturnsConflict()
    {
        // Arrange
        var maxLoans = _member.MembershipPlan!.MaxConcurrentLoans;
        _member.ActiveLoansCount = maxLoans;

        // Act
        var result = await _sut.BorrowAsync(MemberId, CopyId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.Conflict);
        Assert.Equal(CopyStatus.Available, _copy.Status);
        Assert.Equal(maxLoans, _member.ActiveLoansCount);
        VerifyNotSaved();
    }

    [Fact]
    public async Task BorrowAsync_HasOverdueLoan_ReturnsConflict()
    {
        // Arrange
        // בדיקת התאריך עצמה (DueAtUtc לפני עכשיו, ReturnedAtUtc ריק) נעשית בשאילתה ב-HasOverdueLoanAsync של ה-repository.
        // הבדיקה הזאת מוודאת רק את התגובה של ה-service כשה-repository מדווח שיש השאלה באיחור.
        _loans.Setup(r => r.HasOverdueLoanAsync(MemberId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.BorrowAsync(MemberId, CopyId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.Conflict);
        Assert.Equal(CopyStatus.Available, _copy.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task BorrowAsync_ConcurrencyConflictOnSave_ReturnsConflict()
    {
        // Arrange
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("The copy was changed by another request."));

        // Act
        var result = await _sut.BorrowAsync(MemberId, CopyId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.Conflict);
        Assert.Contains("נתפס", result.Error!.Message);
        // The save was attempted once and failed; the service must not retry it on the same context.
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BorrowAsync_CopyRetired_ReturnsNotFound()
    {
        // Arrange
        _copy.Status = CopyStatus.Retired;

        // Act
        var result = await _sut.BorrowAsync(MemberId, CopyId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.NotFound);
        VerifyNotSaved();
    }

    [Fact]
    public async Task BorrowAsync_BookDeleted_ReturnsNotFound()
    {
        // Arrange
        _book.IsDeleted = true;

        // Act
        var result = await _sut.BorrowAsync(MemberId, CopyId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.NotFound);
        Assert.Equal(CopyStatus.Available, _copy.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task ReturnAsync_LoanOfAnotherMember_ReturnsForbidden()
    {
        // Arrange
        var loan = SetupActiveLoan(OtherMemberId);

        // Act
        var result = await _sut.ReturnAsync(MemberId, isLibrarian: false, LoanId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.Forbidden);
        Assert.Null(loan.ReturnedAtUtc);
        Assert.Equal(CopyStatus.Loaned, _copy.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task ReturnAsync_LibrarianReturnsMemberLoan_DecrementsOwnerCount()
    {
        // Arrange
        var loan = SetupActiveLoan(MemberId);
        _member.ActiveLoansCount = 1;

        // Act
        var result = await _sut.ReturnAsync(LibrarianId, isLibrarian: true, LoanId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(loan.ReturnedAtUtc);
        Assert.Equal(CopyStatus.Available, _copy.Status);
        Assert.Equal(0, _member.ActiveLoansCount);

        // The counter belongs to the loan's owner, not to the librarian who returned it.
        _users.Verify(r => r.GetForUpdateAsync(MemberId, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.GetForUpdateAsync(LibrarianId, It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// An active loan of <paramref name="ownerId"/> on the copy (borrowed 3 days ago, due in 11 days).
    /// Moves the copy to Loaned and makes the repository return the loan.
    /// </summary>
    private Loan SetupActiveLoan(int ownerId)
    {
        var nowUtc = DateTime.UtcNow;
        var loan = new Loan
        {
            Id = LoanId,
            UserId = ownerId,
            BookCopyId = CopyId,
            BookCopy = _copy,
            LoanedAtUtc = nowUtc.AddDays(-3),
            DueAtUtc = nowUtc.AddDays(11),
            ReturnedAtUtc = null,
        };

        _copy.Status = CopyStatus.Loaned;
        _loans.Setup(r => r.GetForUpdateAsync(LoanId, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        return loan;
    }

    private static void AssertError(Result result, ErrorType expectedType)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expectedType, result.Error!.Type);
    }

    private void VerifyNotSaved() =>
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
