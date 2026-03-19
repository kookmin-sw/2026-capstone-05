namespace GameServer.Application.DTOs.Requests;

public sealed record SignupRequest(
    string Email,
    string Password,
    string? Nickname
);
