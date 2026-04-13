namespace GameServer.Application.DTOs.Requests;

public sealed record SignupRequest(
    string Email,
    string? Nickname,
    string Password,
    string ConfirmPassword
);
