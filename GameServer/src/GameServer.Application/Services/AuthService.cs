using System;
using System.Text.RegularExpressions;
using GameServer.Application.DTOs.Requests;
using GameServer.Application.DTOs.Responses;
using GameServer.Application.Interfaces;
using GameServer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Services;

public sealed class AuthService
{
    private static readonly Regex NicknameRegex = new("^[a-zA-Z0-9가-힣_]+$", RegexOptions.Compiled);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<AuthResult<UserResponse>> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[AuthService] Signup 처리 시작. email={Email}", request.Email);

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword) ||
            string.IsNullOrWhiteSpace(request.Nickname))
        {
            _logger.LogWarning("[AuthService] Signup 유효성 실패: 필수 입력 누락. email={Email}", request.Email);
            return AuthResult<UserResponse>.Fail(AuthFailureCode.InvalidInput, "email/nickname/password/confirmPassword는 필수입니다.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedNickname = request.Nickname.Trim();

        if (!normalizedEmail.Contains('@'))
        {
            _logger.LogWarning("[AuthService] Signup 유효성 실패: 이메일 형식 오류. email={Email}", request.Email);
            return AuthResult<UserResponse>.Fail(AuthFailureCode.InvalidInput, "email 형식이 올바르지 않습니다. (@ 포함 필수)");
        }

        if (normalizedNickname.Length < 2 || normalizedNickname.Length > 20)
        {
            _logger.LogWarning("[AuthService] Signup 유효성 실패: 닉네임 길이 오류. nickname={Nickname}", normalizedNickname);
            return AuthResult<UserResponse>.Fail(AuthFailureCode.InvalidInput, "nickname은 2~20자여야 합니다.");
        }

        if (!NicknameRegex.IsMatch(normalizedNickname))
        {
            _logger.LogWarning("[AuthService] Signup 유효성 실패: 닉네임 문자 규칙 오류. nickname={Nickname}", normalizedNickname);
            return AuthResult<UserResponse>.Fail(AuthFailureCode.InvalidInput, "nickname은 한글/영문/숫자/_ 만 허용됩니다.");
        }

        if (request.Password.Length < 4)
        {
            _logger.LogWarning("[AuthService] Signup 유효성 실패: 비밀번호 길이 부족. email={Email}", normalizedEmail);
            return AuthResult<UserResponse>.Fail(AuthFailureCode.InvalidInput, "password는 최소 4자 이상이어야 합니다.");
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            _logger.LogWarning("[AuthService] Signup 유효성 실패: 비밀번호 불일치. email={Email}", normalizedEmail);
            return AuthResult<UserResponse>.Fail(AuthFailureCode.InvalidInput, "비밀번호와 비밀번호 재입력이 일치하지 않습니다.");
        }

        try
        {
            var exists = await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken);
            if (exists)
            {
                _logger.LogWarning("[AuthService] Signup 중복 이메일. email={Email}", normalizedEmail);
                return AuthResult<UserResponse>.Fail(AuthFailureCode.DuplicateUsername, "이미 존재하는 email 입니다.");
            }

            var duplicateNickname = await _userRepository.ExistsByNicknameAsync(normalizedNickname, cancellationToken);
            if (duplicateNickname)
            {
                _logger.LogWarning("[AuthService] Signup 중복 닉네임. nickname={Nickname}", normalizedNickname);
                return AuthResult<UserResponse>.Fail(AuthFailureCode.DuplicateNickname, "이미 존재하는 nickname 입니다.");
            }

            var user = new User
            {
                Email = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(request.Password),
                Nickname = normalizedNickname,
                Role = 0,
                Status = 1
            };

            var created = await _userRepository.AddAsync(user, cancellationToken);

            var response = new UserResponse(created.Id, created.Email, created.Nickname, created.Role);
            _logger.LogInformation("[AuthService] Signup DB 저장 성공. userId={UserId}, email={Email}", created.Id, created.Email);
            return AuthResult<UserResponse>.Success(response, "회원가입 성공");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService] Signup DB 저장 실패. email={Email}", normalizedEmail);
            return AuthResult<UserResponse>.Fail(AuthFailureCode.DatabaseError, $"회원가입 DB 오류: {ex.Message}");
        }
    }

    public async Task<AuthResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[AuthService] Login 처리 시작. email={Email}", request.Email);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("[AuthService] Login 유효성 실패: 필수 입력 누락. email={Email}", request.Email);
            return AuthResult<LoginResponse>.Fail(AuthFailureCode.InvalidInput, "email/password는 필수입니다.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (!normalizedEmail.Contains('@'))
        {
            _logger.LogWarning("[AuthService] Login 유효성 실패: 이메일 형식 오류. email={Email}", request.Email);
            return AuthResult<LoginResponse>.Fail(AuthFailureCode.InvalidInput, "email 형식이 올바르지 않습니다. (@ 포함 필수)");
        }

        try
        {
            var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("[AuthService] Login 사용자 없음. email={Email}", normalizedEmail);
                return AuthResult<LoginResponse>.Fail(AuthFailureCode.UserNotFound, "계정이 존재하지 않습니다.");
            }

            var verified = _passwordHasher.Verify(request.Password, user.PasswordHash);
            if (!verified)
            {
                _logger.LogWarning("[AuthService] Login 비밀번호 불일치. email={Email}", normalizedEmail);
                return AuthResult<LoginResponse>.Fail(AuthFailureCode.WrongPassword, "비밀번호 불일치");
            }

            var token = _jwtTokenService.CreateAccessToken(user);
            var responseUser = new UserResponse(user.Id, user.Email, user.Nickname, user.Role);
            var response = new LoginResponse(token, responseUser);
            _logger.LogInformation("[AuthService] Login 성공. userId={UserId}, email={Email}", user.Id, user.Email);
            return AuthResult<LoginResponse>.Success(response, "로그인 성공");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService] Login DB 조회 실패. email={Email}", normalizedEmail);
            return AuthResult<LoginResponse>.Fail(AuthFailureCode.DatabaseError, $"로그인 DB 오류: {ex.Message}");
        }
    }
}
