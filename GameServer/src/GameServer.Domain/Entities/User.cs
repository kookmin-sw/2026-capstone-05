using GameServer.Domain.Common;

namespace GameServer.Domain.Entities;

public sealed class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public int Role { get; set; }
    public int Status { get; set; } = 1;
}
