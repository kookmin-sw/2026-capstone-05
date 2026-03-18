using GameServer.Application.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // TODO: query users table by email
        _ = email;
        _ = cancellationToken;
        return Task.FromResult(false);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // TODO: query users table by email
        _ = email;
        _ = cancellationToken;
        return Task.FromResult<User?>(null);
    }

    public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        // TODO: insert into users table and return created row
        _ = cancellationToken;
        return Task.FromResult(user);
    }
}
