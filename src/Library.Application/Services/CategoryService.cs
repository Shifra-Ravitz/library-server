using AutoMapper;
using Library.Application.Common;
using Library.Application.Common.Exceptions;
using Library.Application.DTOs.Categories;
using Library.Application.Interfaces;
using Library.Domain.Entities;

namespace Library.Application.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CategoryService(ICategoryRepository categories, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct)
    {
        var categories = await _categories.GetAllAsync(ct);
        return _mapper.Map<List<CategoryResponse>>(categories);
    }

    public async Task<Result<CategoryResponse>> GetByIdAsync(int id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null)
        {
            return CategoryNotFound(id);
        }

        return _mapper.Map<CategoryResponse>(category);
    }

    public async Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();

        // The early check gives a clear message in the normal case; the unique index covers concurrent requests.
        if (await _categories.NameExistsAsync(name, null, ct))
        {
            return DuplicateName(name);
        }

        var category = _mapper.Map<Category>(request);
        category.Name = name;
        _categories.Add(category);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DuplicateValueException)
        {
            return DuplicateName(name);
        }

        return _mapper.Map<CategoryResponse>(category);
    }

    public async Task<Result> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var category = await _categories.GetForUpdateAsync(id, ct);
        if (category is null)
        {
            return CategoryNotFound(id);
        }

        var name = request.Name.Trim();
        if (await _categories.NameExistsAsync(name, id, ct))
        {
            return DuplicateName(name);
        }

        _mapper.Map(request, category);
        category.Name = name;

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DuplicateValueException)
        {
            return DuplicateName(name);
        }

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var category = await _categories.GetForUpdateAsync(id, ct);
        if (category is null)
        {
            return CategoryNotFound(id);
        }

        // Physical delete; the book links are removed by the cascade on the join table.
        _categories.Remove(category);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static Error CategoryNotFound(int id) => Error.NotFound($"קטגוריה עם מזהה {id} לא נמצאה.");

    private static Error DuplicateName(string name) => Error.Conflict($"קטגוריה בשם '{name}' כבר קיימת.");
}
