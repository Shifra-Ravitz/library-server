using System.Diagnostics.CodeAnalysis;
using AutoMapper;
using Library.Application.Common;
using Library.Application.DTOs.Copies;
using Library.Application.Interfaces;
using Library.Domain.Entities;
using Library.Domain.Enums;

namespace Library.Application.Services;

public sealed class BookCopyService : IBookCopyService
{
    private readonly IBookCopyRepository _copies;
    private readonly IBookRepository _books;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public BookCopyService(IBookCopyRepository copies, IBookRepository books, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _copies = copies;
        _books = books;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<BookCopyResponse>>> GetByBookIdAsync(int bookId, CancellationToken ct)
    {
        var book = await _books.GetByIdAsync(bookId, ct);
        if (book is null || book.IsDeleted)
        {
            return BookNotFound(bookId);
        }

        var copies = await _copies.GetByBookIdAsync(bookId, ct);
        var responses = _mapper.Map<List<BookCopyResponse>>(copies);

        // Explicit Success: the implicit conversion from T is not applied when T is an interface.
        return Result<IReadOnlyList<BookCopyResponse>>.Success(responses);
    }

    public async Task<Result<BookCopyResponse>> GetByIdAsync(int id, CancellationToken ct)
    {
        var copy = await _copies.GetByIdAsync(id, ct);
        if (IsMissing(copy))
        {
            return CopyNotFound(id);
        }

        return _mapper.Map<BookCopyResponse>(copy);
    }

    public async Task<Result<BookCopyResponse>> CreateAsync(int bookId, CancellationToken ct)
    {
        // Tracked, so EF treats the book as an existing row (Unchanged) and only inserts the copy.
        var book = await _books.GetForUpdateAsync(bookId, ct);
        if (book is null || book.IsDeleted)
        {
            return BookNotFound(bookId);
        }

        var copy = new BookCopy { Book = book, Status = CopyStatus.Available };
        _copies.Add(copy);

        await _unitOfWork.SaveChangesAsync(ct);

        return _mapper.Map<BookCopyResponse>(copy);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var copy = await _copies.GetForUpdateAsync(id, ct);
        if (IsMissing(copy))
        {
            return CopyNotFound(id);
        }

        if (copy.Status == CopyStatus.Loaned)
        {
            return Error.Conflict("לא ניתן להוציא משימוש עותק שמושאל כרגע.");
        }

        // Soft delete: the row stays, so the loan history keeps pointing at it.
        copy.Status = CopyStatus.Retired;

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// A Retired copy, or a copy of a deleted book, is treated exactly like a copy that does not exist.
    /// Requires <c>copy.Book</c> to be loaded.
    /// </summary>
    private static bool IsMissing([NotNullWhen(false)] BookCopy? copy) =>
        copy is null || copy.Status == CopyStatus.Retired || copy.Book.IsDeleted;

    private static Error BookNotFound(int id) => Error.NotFound($"ספר עם מזהה {id} לא נמצא.");

    private static Error CopyNotFound(int id) => Error.NotFound($"עותק עם מזהה {id} לא נמצא.");
}
