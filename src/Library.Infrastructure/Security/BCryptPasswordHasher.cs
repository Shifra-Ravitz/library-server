using Library.Application.Interfaces;

namespace Library.Infrastructure.Security;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    // 2^11 rounds. The work factor is stored inside each hash, so raising it later does not break existing hashes.
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
