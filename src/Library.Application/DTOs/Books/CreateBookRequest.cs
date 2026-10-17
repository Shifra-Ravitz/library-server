using System.ComponentModel.DataAnnotations;

namespace Library.Application.DTOs.Books;

/// <summary>
/// Body of POST /api/books. String lengths match BookConfiguration.
/// </summary>
public sealed class CreateBookRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// The lower bound is below the earliest seeded year (1489).
    /// </summary>
    [Range(1000, 2100)]
    public int PublicationYear { get; set; }

    /// <summary>
    /// Ids that do not exist are rejected by the service, not here.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public List<int> CategoryIds { get; set; } = [];
}
