namespace GameServer.Application.DTOs.Responses;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string? Nickname,
    int Role
);
