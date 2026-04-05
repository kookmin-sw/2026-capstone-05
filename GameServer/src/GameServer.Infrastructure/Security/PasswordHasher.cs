using GameServer.Application.Interfaces;

namespace GameServer.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        // TODO: replace with BCrypt/Argon2 implementation
        return password;
    }

    public bool Verify(string password, string passwordHash)
    {
        // TODO: replace with BCrypt/Argon2 verification
        return password == passwordHash;
    }
}
