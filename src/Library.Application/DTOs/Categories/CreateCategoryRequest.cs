using System.ComponentModel.DataAnnotations;

namespace Library.Application.DTOs.Categories;

/// <summary>
/// Body of POST /api/categories. Name uniqueness is checked by the service.
/// </summary>
public sealed class CreateCategoryRequest
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;
}
