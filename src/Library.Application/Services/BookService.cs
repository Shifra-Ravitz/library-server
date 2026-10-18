using AutoMapper;
using Library.Application.Common;
using Library.Application.Common.Exceptions;
using Library.Application.DTOs.Books;
using Library.Application.Interfaces;
using Library.Domain.Entities;
using Library.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Library.Application.Services;

public sealed class BookService : IBookService
{
    private const string DeleteConflictMessage = "הספר עודכן בינתיים (ייתכן שעותק שלו הושאל ברגע זה). נסי שוב.";

    private readonly IBookRepository _books;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<BookService> _logger;

    public BookService(
        IBookRepository books,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<BookService> logger)
    {
        _books = books;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResult<BookResponse>> SearchAsync(BookQueryParameters query, CancellationToken ct)
    {
        // Built by hand: the filter is an internal contract with the repository, not a DTO.
        var filter = new BookSearchFilter
        {
            Title = NormalizeText(query.Title),
            Author = NormalizeText(query.Author),
            CategoryId = query.CategoryId,
            HasAvailableCopy = query.HasAvailableCopy,
            SortBy = query.SortBy,
            Descending = query.Descending,
            Page = query.Page,
            PageSize = query.PageSize,
        };

        var (items, totalCount) = await _books.SearchAsync(filter, ct);
        var responses = _mapper.Map<List<BookResponse>>(items);

        return new PagedResult<BookResponse>(responses, filter.Page, filter.PageSize, totalCount);
    }

    public async Task<Result<BookResponse>> GetByIdAsync(int id, CancellationToken ct)
    {
        var book = await _books.GetByIdAsync(id, ct);
        if (book is null || book.IsDeleted)
        {
            return BookNotFound(id);
        }

        return _mapper.Map<BookResponse>(book);
    }

    public async Task<Result<BookResponse>> CreateAsync(CreateBookRequest request, CancellationToken ct)
    {
        var categoriesResult = await LoadCategoriesAsync(request.CategoryIds, ct);
        if (categoriesResult.IsFailure)
        {
            return categoriesResult.Error;
        }

        var book = _mapper.Map<Book>(request);
        foreach (var category in categoriesResult.Value)
        {
            book.Categories.Add(category);
        }

        _books.Add(book);
        await _unitOfWork.SaveChangesAsync(ct);

        return _mapper.Map<BookResponse>(book);
    }

    public async Task<Result> UpdateAsync(int id, UpdateBookRequest request, CancellationToken ct)
    {
        var book = await _books.GetWithCategoriesForUpdateAsync(id, ct);
        if (book is null || book.IsDeleted)
        {
            return BookNotFound(id);
        }

        var categoriesResult = await LoadCategoriesAsync(request.CategoryIds, ct);
        if (categoriesResult.IsFailure)
        {
            return categoriesResult.Error;
        }

        _mapper.Map(request, book);
        SyncCategories(book, categoriesResult.Value);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var book = await _books.GetWithCopiesForUpdateAsync(id, ct);
        if (book is null || book.IsDeleted)
        {
            return BookNotFound(id);
        }

        if (book.Copies.Any(c => c.Status == CopyStatus.Loaned))
        {
            return Error.Conflict("לא ניתן למחוק ספר שיש לו עותק מושאל כרגע.");
        }

        // Soft delete: the rows stay, so the loan history keeps pointing at them.
        book.IsDeleted = true;
        foreach (var copy in book.Copies.Where(c => c.Status != CopyStatus.Retired))
        {
            copy.Status = CopyStatus.Retired;
        }

        // Retiring the copies updates them with their Version in the WHERE. If one of them was borrowed
        // after it was read here, that UPDATE matches no row and IsDeleted is rolled back with it.
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning("Concurrency conflict while deleting book {BookId}", id);
            return Error.Conflict(DeleteConflictMessage);
        }

        return Result.Success();
    }

    /// <summary>
    /// Loads the requested categories tracked, so EF links the existing rows instead of inserting new ones.
    /// Fails with Validation when any id does not exist.
    /// </summary>
    private async Task<Result<List<Category>>> LoadCategoriesAsync(IEnumerable<int> categoryIds, CancellationToken ct)
    {
        var ids = categoryIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<Category>();
        }

        var categories = await _categories.GetByIdsForUpdateAsync(ids, ct);

        var foundIds = categories.Select(c => c.Id).ToHashSet();
        var missingIds = ids.Where(categoryId => !foundIds.Contains(categoryId)).ToList();
        if (missingIds.Count > 0)
        {
            return Error.Validation($"הקטגוריות הבאות לא קיימות: {string.Join(", ", missingIds)}.");
        }

        return categories.ToList();
    }

    /// <summary>
    /// Removes only the categories that are no longer requested and adds only the new ones,
    /// so unchanged join rows are left untouched.
    /// </summary>
    private static void SyncCategories(Book book, IReadOnlyCollection<Category> categories)
    {
        var requestedIds = categories.Select(c => c.Id).ToHashSet();
        foreach (var removed in book.Categories.Where(c => !requestedIds.Contains(c.Id)).ToList())
        {
            book.Categories.Remove(removed);
        }

        var currentIds = book.Categories.Select(c => c.Id).ToHashSet();
        foreach (var added in categories.Where(c => !currentIds.Contains(c.Id)))
        {
            book.Categories.Add(added);
        }
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error BookNotFound(int id) => Error.NotFound($"ספר עם מזהה {id} לא נמצא.");
}
