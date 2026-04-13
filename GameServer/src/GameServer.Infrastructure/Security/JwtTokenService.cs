using GameServer.Application.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    public string CreateAccessToken(User user)
    {
        // TODO: 실제 JWT 서명 로직으로 교체
        return $"dev-token-{user.Id}";
    }
}
