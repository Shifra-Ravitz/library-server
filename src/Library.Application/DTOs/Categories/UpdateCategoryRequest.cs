using System.ComponentModel.DataAnnotations;

namespace Library.Application.DTOs.Categories;

/// <summary>
/// Body of PUT /api/categories/{id}. Name uniqueness is checked by the service.
/// </summary>
public sealed class UpdateCategoryRequest
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;
}
