using System.ComponentModel.DataAnnotations;

namespace Library.Application.DTOs.Auth;

/// <summary>
/// Body of POST /api/auth/register. Email uniqueness and plan existence are checked by the service.
/// </summary>
public sealed class RegisterRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Not stored (only its hash is), so this length is a policy, not a column size.
    /// BCrypt uses only the first 72 bytes of the password.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int MembershipPlanId { get; set; }
}
