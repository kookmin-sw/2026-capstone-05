using GameServer.Application.DTOs.Requests;
using GameServer.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken cancellationToken)
    {
        var created = await _authService.SignupAsync(request, cancellationToken);
        return Ok(created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var loggedIn = await _authService.LoginAsync(request, cancellationToken);
        if (loggedIn is null)
        {
            return Unauthorized();
        }

        return Ok(loggedIn);
    }
}
