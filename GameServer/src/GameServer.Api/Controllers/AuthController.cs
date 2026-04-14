using GameServer.Application.DTOs.Requests;
using GameServer.Application.DTOs.Responses;
using GameServer.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameServer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[AuthController] Signup 요청 수신. traceId={TraceId}, email={Email}",
            HttpContext.TraceIdentifier,
            request.Email);

        var result = await _authService.SignupAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "[AuthController] Signup 성공 응답. traceId={TraceId}, email={Email}",
                HttpContext.TraceIdentifier,
                request.Email);
            return Ok(result.Data);
        }

        _logger.LogWarning(
            "[AuthController] Signup 실패 응답. traceId={TraceId}, code={FailureCode}, message={Message}",
            HttpContext.TraceIdentifier,
            result.FailureCode,
            result.Message);

        return result.FailureCode switch
        {
            AuthFailureCode.InvalidInput => BadRequest(result.Message),
            AuthFailureCode.DuplicateUsername => Conflict(result.Message),
            AuthFailureCode.DatabaseError => StatusCode(500, result.Message),
            _ => BadRequest(result.Message)
        };
    }


    [HttpGet("db-ping")]
    public async Task<IActionResult> DbPing(CancellationToken cancellationToken)
    {
        var probeEmail = "__db_probe_user__@example.com";
        var result = await _authService.LoginAsync(new LoginRequest(probeEmail, "invalid"), cancellationToken);

        if (result.FailureCode == AuthFailureCode.DatabaseError)
            return StatusCode(500, result.Message);

        return Ok("DB connection OK");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[AuthController] Login 요청 수신. traceId={TraceId}, email={Email}",
            HttpContext.TraceIdentifier,
            request.Email);

        var result = await _authService.LoginAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "[AuthController] Login 성공 응답. traceId={TraceId}, email={Email}",
                HttpContext.TraceIdentifier,
                request.Email);
            return Ok(result.Data);
        }

        _logger.LogWarning(
            "[AuthController] Login 실패 응답. traceId={TraceId}, code={FailureCode}, message={Message}",
            HttpContext.TraceIdentifier,
            result.FailureCode,
            result.Message);

        return result.FailureCode switch
        {
            AuthFailureCode.InvalidInput => BadRequest(result.Message),
            AuthFailureCode.UserNotFound => NotFound(result.Message),
            AuthFailureCode.WrongPassword => Unauthorized(result.Message),
            AuthFailureCode.DatabaseError => StatusCode(500, result.Message),
            _ => Unauthorized(result.Message)
        };
    }
}
