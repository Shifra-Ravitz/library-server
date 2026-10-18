using Library.Domain.Enums;

namespace Library.Application.DTOs.Auth;

/// <summary>
/// Returned by register and login. The client sends Token as "Authorization: Bearer {Token}".
/// </summary>
public sealed class AuthResponse
{
    public string Token { get; init; } = string.Empty;

    public DateTime ExpiresAtUtc { get; init; }

    public int UserId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public UserRole Role { get; init; }
}
