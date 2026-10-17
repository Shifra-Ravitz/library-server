using System.ComponentModel.DataAnnotations;

namespace Library.Application.DTOs.Books;

/// <summary>
/// Body of PUT /api/books/{id}. String lengths match BookConfiguration.
/// </summary>
public sealed class UpdateBookRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// The lower bound is below the earliest seeded year (1489), so seeded books can be updated.
    /// </summary>
    [Range(1000, 2100)]
    public int PublicationYear { get; set; }

    /// <summary>
    /// Replaces the book's categories. Ids that do not exist are rejected by the service, not here.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public List<int> CategoryIds { get; set; } = [];
}
