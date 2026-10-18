namespace Library.Application.Interfaces;

/// <summary>
/// One-way password hashing. Implemented in Infrastructure, so Application does not depend on a hashing library.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
