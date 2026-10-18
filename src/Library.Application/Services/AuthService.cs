using System.Text;
using Library.Application.Common;
using Library.Application.Common.Exceptions;
using Library.Application.DTOs.Auth;
using Library.Application.Interfaces;
using Library.Domain.Entities;
using Library.Domain.Enums;

namespace Library.Application.Services;

public sealed class AuthService : IAuthService
{
    // BCrypt silently ignores everything after the first 72 bytes of the password.
    private const int BCryptMaxBytes = 72;

    private const string InvalidCredentialsMessage = "האימייל או הסיסמה שגויים.";

    // A real BCrypt hash (same work factor as stored hashes) that is verified when the email does not exist.
    // Created once on first use; if two requests race, both compute a valid hash and either one is kept.
    private static string? s_dummyHash;

    private readonly IUserRepository _users;
    private readonly IMembershipPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(
        IUserRepository users,
        IMembershipPlanRepository plans,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _users = users;
        _plans = plans;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (Encoding.UTF8.GetByteCount(request.Password) > BCryptMaxBytes)
        {
            return Error.Validation($"הסיסמה ארוכה מדי: מותר עד {BCryptMaxBytes} בתים (אות עברית תופסת 2 בתים).");
        }

        if (!await _plans.ExistsAsync(request.MembershipPlanId, ct))
        {
            return Error.Validation($"סוג מנוי עם מזהה {request.MembershipPlanId} לא קיים.");
        }

        var email = NormalizeEmail(request.Email);

        // The early check gives a clear message in the normal case; the unique index covers concurrent requests.
        if (await _users.EmailExistsAsync(email, ct))
        {
            return EmailTaken();
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            // Always a member: the request has no Role, so open registration cannot create a librarian.
            Role = UserRole.Member,
            MembershipPlanId = request.MembershipPlanId,
        };
        _users.Add(user);

        // Saved before the token is created, so user.Id is set.
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DuplicateValueException)
        {
            return EmailTaken();
        }

        return CreateResponse(user);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        // No stored password can be longer, so this is a wrong password, reported with the same message.
        if (Encoding.UTF8.GetByteCount(request.Password) > BCryptMaxBytes)
        {
            return Error.Unauthorized(InvalidCredentialsMessage);
        }

        var user = await _users.GetByEmailAsync(NormalizeEmail(request.Email), ct);
        if (user is null)
        {
            // Verifying against a dummy hash makes an unknown email take about as long as a wrong password,
            // which narrows the response-time gap that would reveal which emails are registered.
            // It does not remove the gap entirely (the database lookup and the rest of the path still differ).
            s_dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString());
            _passwordHasher.Verify(request.Password, s_dummyHash);
            return Error.Unauthorized(InvalidCredentialsMessage);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Error.Unauthorized(InvalidCredentialsMessage);
        }

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = _tokenGenerator.Generate(user);

        return new AuthResponse
        {
            Token = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
        };
    }

    // The unique index on Email is case-sensitive in PostgreSQL, so emails are always stored and looked up in lower case.
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static Error EmailTaken() => Error.Conflict("כתובת האימייל כבר רשומה במערכת.");
}
