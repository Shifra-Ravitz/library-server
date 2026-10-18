using AutoMapper;
using Library.Application.Common;
using Library.Application.Common.Exceptions;
using Library.Application.Interfaces;
using Library.Application.Services;
using Library.Domain.Entities;
using Library.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Library.Tests.Services;

public class BookServiceTests
{
    private const int BookId = 10;

    private readonly Mock<IBookRepository> _books = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Book _book;
    private readonly BookCopy _availableCopy1;
    private readonly BookCopy _availableCopy2;
    private readonly BookCopy _retiredCopy;

    private readonly BookService _sut;

    // הבנאי בונה מצב "הכול תקין": ספר לא מחוק עם שלושה עותקים (שניים Available ואחד Retired),
    // שנטען עם tracking דרך GetWithCopiesForUpdateAsync. כל בדיקה משנה רק את הדבר שהיא בודקת.
    // זה עובד כי xUnit יוצר מופע חדש של מחלקת הבדיקות לכל בדיקה, כך שאין מצב משותף בין בדיקות.
    public BookServiceTests()
    {
        _book = new Book { Id = BookId, Title = "ספר לבדיקה", Author = "מחברת", IsDeleted = false };
        _availableCopy1 = new BookCopy { Id = 101, BookId = BookId, Book = _book, Status = CopyStatus.Available };
        _availableCopy2 = new BookCopy { Id = 102, BookId = BookId, Book = _book, Status = CopyStatus.Available };
        _retiredCopy = new BookCopy { Id = 103, BookId = BookId, Book = _book, Status = CopyStatus.Retired };
        _book.Copies.Add(_availableCopy1);
        _book.Copies.Add(_availableCopy2);
        _book.Copies.Add(_retiredCopy);

        _books.Setup(r => r.GetWithCopiesForUpdateAsync(BookId, It.IsAny<CancellationToken>())).ReturnsAsync(_book);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // The real profiles from Library.Application, not a mock of IMapper.
        var mapperConfig = new MapperConfiguration(
            cfg => cfg.AddMaps(typeof(BookService).Assembly),
            NullLoggerFactory.Instance);

        _sut = new BookService(
            _books.Object,
            _categories.Object,
            _unitOfWork.Object,
            mapperConfig.CreateMapper(),
            NullLogger<BookService>.Instance);
    }

    [Fact]
    public async Task DeleteAsync_NoLoanedCopies_MarksDeletedAndRetiresAllCopies()
    {
        // Arrange: nothing to change, the constructor already built the valid state.

        // Act
        var result = await _sut.DeleteAsync(BookId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(_book.IsDeleted);
        Assert.All(_book.Copies, copy => Assert.Equal(CopyStatus.Retired, copy.Status));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // אין Verify על Remove/Delete: ל-IBookRepository ול-IBookCopyRepository אין בכלל מתודת מחיקה
        // (רק Add), כך שהממשק עצמו מבטיח שה-service לא יכול למחוק שורה פיזית, רק לסמן IsDeleted ו-Retired.
    }

    [Fact]
    public async Task DeleteAsync_HasLoanedCopy_ReturnsConflictAndChangesNothing()
    {
        // Arrange
        _availableCopy1.Status = CopyStatus.Loaned;

        // Act
        var result = await _sut.DeleteAsync(BookId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.Conflict);
        Assert.False(_book.IsDeleted);
        // No partial change: the other copy was not retired before the loaned copy was found.
        Assert.Equal(CopyStatus.Available, _availableCopy2.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task DeleteAsync_BookAlreadyDeleted_ReturnsNotFound()
    {
        // Arrange
        _book.IsDeleted = true;

        // Act
        var result = await _sut.DeleteAsync(BookId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.NotFound);
        VerifyNotSaved();
    }

    [Fact]
    public async Task DeleteAsync_ConcurrencyConflictOnSave_ReturnsConflict()
    {
        // Arrange
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("A copy was changed by another request."));

        // Act
        var result = await _sut.DeleteAsync(BookId, CancellationToken.None);

        // Assert
        AssertError(result, ErrorType.Conflict);
    }

    private static void AssertError(Result result, ErrorType expectedType)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expectedType, result.Error!.Type);
    }

    private void VerifyNotSaved() =>
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
