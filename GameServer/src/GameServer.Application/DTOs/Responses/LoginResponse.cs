namespace GameServer.Application.DTOs.Responses;

public sealed record LoginResponse(
    string AccessToken,
    UserResponse User
);
