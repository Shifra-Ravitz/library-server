using Library.Application.Common;
using Library.Application.DTOs.Categories;

namespace Library.Application.Interfaces;

/// <summary>
/// Category operations. Names are unique; categories are deleted physically.
/// </summary>
public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct);

    Task<Result<CategoryResponse>> GetByIdAsync(int id, CancellationToken ct);

    Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct);

    Task<Result> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct);

    Task<Result> DeleteAsync(int id, CancellationToken ct);
}
