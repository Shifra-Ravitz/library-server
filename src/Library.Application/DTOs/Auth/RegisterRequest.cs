using System.ComponentModel.DataAnnotations;

namespace Library.Application.DTOs.Auth;

/// <summary>
/// Body of POST /api/auth/register. Email uniqueness and plan existence are checked by the service.
/// There is deliberately no Role: registration is open, so the role is always set by the server.
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
    /// BCrypt uses only the first 72 bytes of the password. This counts characters; the service also
    /// checks the UTF-8 byte count, since a Hebrew letter is 2 bytes.
    /// </summary>
    [Required]
    [StringLength(72, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int MembershipPlanId { get; set; }
}
