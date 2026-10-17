using Library.Application.DTOs.Users;

namespace Library.Application.DTOs.Auth;

public sealed class AuthResponse
{
    public string Token { get; init; } = string.Empty;

    public DateTime ExpiresAtUtc { get; init; }

    public UserResponse User { get; init; } = new();
}
