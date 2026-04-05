using GameServer.Application.DTOs.Requests;
using GameServer.Application.DTOs.Responses;
using GameServer.Application.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Application.Services;

public sealed class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<UserResponse> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default)
    {
        // TODO: validate input + duplicate email check + save user
        var passwordHash = _passwordHasher.Hash(request.Password);

        var user = new User(); // TODO: map real fields
        _ = passwordHash;

        var created = await _userRepository.AddAsync(user, cancellationToken);
        return new UserResponse(created.Id, request.Email, request.Nickname, 0);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // TODO: load user by email + verify password + role/status checks
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // TODO: use actual stored hash once entity is implemented
        var verified = _passwordHasher.Verify(request.Password, "");
        if (!verified)
        {
            return null;
        }

        var token = _jwtTokenService.CreateAccessToken(user);
        var responseUser = new UserResponse(user.Id, request.Email, null, 0);
        return new LoginResponse(token, responseUser);
    }
}
