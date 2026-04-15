using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AuthService
{
    private const string LogPrefix = "[06_Backend][AuthService]";
    private readonly string _baseUrl;

    public AuthService(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public IEnumerator Signup(string email, string nickname, string password, string confirmPassword, Action<AuthResult> onCompleted)
    {
        Debug.Log($"{LogPrefix} 회원가입 API 호출 시도. email={email}");

        if (!TryValidateSignup(email, nickname, password, confirmPassword, out var invalidResult, out var normalizedEmail, out var normalizedNickname))
        {
            onCompleted?.Invoke(invalidResult);
            yield break;
        }

        SignupRequest requestDto = new()
        {
            Email = normalizedEmail,
            Nickname = normalizedNickname,
            Password = password,
            ConfirmPassword = confirmPassword
        };

        string json = JsonUtility.ToJson(requestDto);
        using UnityWebRequest request = BuildJsonPostRequest($"{_baseUrl}/api/auth/signup", json);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            UserResponse user = JsonUtility.FromJson<UserResponse>(request.downloadHandler.text);
            UserAccount created = new()
            {
                Id = user.id,
                Username = user.email,
                Password = password
            };

            Debug.Log($"{LogPrefix} 회원가입 성공. email={created.Username}, userId={created.Id}");
            onCompleted?.Invoke(new AuthResult(AuthResultCode.Success, "회원가입 성공", created));
            yield break;
        }

        Debug.LogError($"{LogPrefix} 회원가입 실패. code={request.responseCode}, body={request.downloadHandler.text}, error={request.error}");
        onCompleted?.Invoke(MapFailure((int)request.responseCode, request.error, request.downloadHandler.text));
    }

    public IEnumerator PingDatabase(Action<AuthResult> onCompleted)
    {
        using UnityWebRequest request = UnityWebRequest.Get($"{_baseUrl}/api/auth/db-ping");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            onCompleted?.Invoke(new AuthResult(AuthResultCode.Success, request.downloadHandler.text));
            yield break;
        }

        Debug.LogError($"{LogPrefix} DB 핑 실패. code={request.responseCode}, body={request.downloadHandler.text}, error={request.error}");
        onCompleted?.Invoke(MapFailure((int)request.responseCode, request.error, request.downloadHandler.text));
    }

    public IEnumerator Login(string email, string password, Action<AuthResult> onCompleted)
    {
        Debug.Log($"{LogPrefix} 로그인 API 호출 시도. email={email}");

        if (!TryValidateLogin(email, password, out var invalidResult, out var normalizedEmail))
        {
            onCompleted?.Invoke(invalidResult);
            yield break;
        }

        LoginRequest requestDto = new() { Email = normalizedEmail, Password = password };
        string json = JsonUtility.ToJson(requestDto);

        using UnityWebRequest request = BuildJsonPostRequest($"{_baseUrl}/api/auth/login", json);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
            UserAccount account = new()
            {
                Id = response.user.id,
                Username = response.user.email,
                Password = password
            };

            AuthSession.SetAuthenticated(account);
            Debug.Log($"{LogPrefix} 로그인 성공. email={account.Username}, userId={account.Id}");
            onCompleted?.Invoke(new AuthResult(AuthResultCode.Success, "로그인 성공", account));
            yield break;
        }

        Debug.LogError($"{LogPrefix} 로그인 실패. code={request.responseCode}, body={request.downloadHandler.text}, error={request.error}");
        onCompleted?.Invoke(MapFailure((int)request.responseCode, request.error, request.downloadHandler.text));
    }

    private static bool TryValidateSignup(
        string email,
        string nickname,
        string password,
        string confirmPassword,
        out AuthResult invalidResult,
        out string normalizedEmail,
        out string normalizedNickname)
    {
        invalidResult = null;
        normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        normalizedNickname = nickname?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedEmail) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirmPassword) ||
            string.IsNullOrWhiteSpace(normalizedNickname))
        {
            invalidResult = new AuthResult(AuthResultCode.InvalidInput, "이메일/닉네임/비밀번호/비밀번호 확인은 필수입니다.");
            return false;
        }

        if (!normalizedEmail.Contains("@"))
        {
            invalidResult = new AuthResult(AuthResultCode.InvalidInput, "이메일 형식이 올바르지 않습니다. (@ 포함 필수)");
            return false;
        }

        if (normalizedNickname.Length < 2 || normalizedNickname.Length > 20)
        {
            invalidResult = new AuthResult(AuthResultCode.InvalidInput, "닉네임은 2~20자여야 합니다.");
            return false;
        }

        if (password.Length < 4)
        {
            invalidResult = new AuthResult(AuthResultCode.InvalidInput, "비밀번호는 최소 4자 이상이어야 합니다.");
            return false;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            invalidResult = new AuthResult(AuthResultCode.InvalidInput, "비밀번호와 비밀번호 확인이 일치하지 않습니다.");
            return false;
        }

        return true;
    }

    private static bool TryValidateLogin(string email, string password, out AuthResult invalidResult, out string normalizedEmail)
    {
        invalidResult = null;
        normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
        {
            invalidResult = new AuthResult(AuthResultCode.InvalidInput, "이메일/비밀번호는 공백일 수 없습니다.");
            return false;
        }

        if (!normalizedEmail.Contains("@"))
        {
            invalidResult = new AuthResult(AuthResultCode.InvalidInput, "이메일 형식이 올바르지 않습니다. (@ 포함 필수)");
            return false;
        }

        return true;
    }

    private static UnityWebRequest BuildJsonPostRequest(string url, string json)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new(url, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(bodyRaw),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private static AuthResult MapFailure(int statusCode, string error, string responseText)
    {
        string serverMessage = ExtractErrorMessage(responseText);

        if (statusCode <= 0)
            return new AuthResult(AuthResultCode.NetworkError, $"네트워크 오류: {error}");

        return statusCode switch
        {
            400 => new AuthResult(AuthResultCode.InvalidInput, string.IsNullOrEmpty(serverMessage) ? "입력값이 올바르지 않습니다." : serverMessage),
            401 => new AuthResult(AuthResultCode.WrongPassword, string.IsNullOrEmpty(serverMessage) ? "비밀번호가 일치하지 않습니다." : serverMessage),
            404 => new AuthResult(AuthResultCode.UserNotFound, string.IsNullOrEmpty(serverMessage) ? "계정이 존재하지 않습니다." : serverMessage),
            409 => new AuthResult(ResolveDuplicateCode(serverMessage), string.IsNullOrEmpty(serverMessage) ? "중복된 계정 정보입니다." : serverMessage),
            _ => new AuthResult(AuthResultCode.DatabaseError, string.IsNullOrEmpty(serverMessage) ? $"DB/API 오류: {error}" : serverMessage)
        };
    }

    private static AuthResultCode ResolveDuplicateCode(string serverMessage)
    {
        if (!string.IsNullOrEmpty(serverMessage) &&
            serverMessage.IndexOf("nick", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return AuthResultCode.DuplicateNickname;
        }

        return AuthResultCode.DuplicateUsername;
    }

    private static string ExtractErrorMessage(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return string.Empty;

        string trimmed = responseText.Trim();

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            return trimmed.Substring(1, trimmed.Length - 2);

        return trimmed;
    }

    [Serializable]
    private sealed class SignupRequest
    {
        public string Email;
        public string Nickname;
        public string Password;
        public string ConfirmPassword;
    }

    [Serializable]
    private sealed class LoginRequest
    {
        public string Email;
        public string Password;
    }

    [Serializable]
    private sealed class LoginResponse
    {
        public string accessToken;
        public UserResponse user;
    }

    [Serializable]
    private sealed class UserResponse
    {
        public string id;
        public string email;
        public string nickname;
        public int role;
    }
}
