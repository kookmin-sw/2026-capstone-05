using GameServer.Application.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    public string CreateAccessToken(User user)
    {
        // TODO: implement JWT signing with claims
        _ = user;
        return string.Empty;
    }
}
