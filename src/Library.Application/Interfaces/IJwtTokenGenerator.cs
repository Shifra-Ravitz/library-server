using Library.Domain.Entities;

namespace Library.Application.Interfaces;

/// <summary>
/// Issues a JWT for a user whose identity has already been verified.
/// </summary>
public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}
