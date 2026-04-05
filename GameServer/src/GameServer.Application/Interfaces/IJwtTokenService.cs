using GameServer.Domain.Entities;

namespace GameServer.Application.Interfaces;

public interface IJwtTokenService
{
    string CreateAccessToken(User user);
}
