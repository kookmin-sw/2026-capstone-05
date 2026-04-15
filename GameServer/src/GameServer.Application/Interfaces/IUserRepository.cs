using GameServer.Domain.Entities;

namespace GameServer.Application.Interfaces;

public interface IUserRepository
{
    // Backward-compatible contract:
    // some local environments still reference *ByUsernameAsync signatures.
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => ExistsByUsernameAsync(email, cancellationToken);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => GetByUsernameAsync(email, cancellationToken);

    Task<bool> ExistsByNicknameAsync(string nickname, CancellationToken cancellationToken = default);

    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
}
