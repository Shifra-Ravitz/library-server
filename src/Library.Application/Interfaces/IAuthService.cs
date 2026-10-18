using Library.Application.Common;
using Library.Application.DTOs.Auth;

namespace Library.Application.Interfaces;

/// <summary>
/// Open registration (always as a member) and login. Both return a token on success.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
}
