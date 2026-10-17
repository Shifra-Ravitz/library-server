using System.ComponentModel.DataAnnotations;

namespace Library.Application.DTOs.Auth;

/// <summary>
/// Body of POST /api/auth/login. No MinimumLength here, so a failed login reveals nothing about the password policy.
/// </summary>
public sealed class LoginRequest
{
    [Required]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}
