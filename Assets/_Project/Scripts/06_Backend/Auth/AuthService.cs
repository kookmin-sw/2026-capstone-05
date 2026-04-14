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

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirmPassword))
        {
            onCompleted?.Invoke(new AuthResult(AuthResultCode.InvalidInput, "이메일/비밀번호/비밀번호 재입력은 공백일 수 없습니다."));
            yield break;
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        if (!normalizedEmail.Contains("@"))
        {
            onCompleted?.Invoke(new AuthResult(AuthResultCode.InvalidInput, "이메일 형식이 올바르지 않습니다. (@ 포함 필수)"));
            yield break;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            onCompleted?.Invoke(new AuthResult(AuthResultCode.InvalidInput, "비밀번호와 비밀번호 재입력이 일치하지 않습니다."));
            yield break;
        }

        SignupRequest requestDto = new SignupRequest
        {
            Email = normalizedEmail,
            Nickname = string.IsNullOrWhiteSpace(nickname) ? normalizedEmail : nickname.Trim(),
            Password = password,
            ConfirmPassword = confirmPassword
        };
        string json = JsonUtility.ToJson(requestDto);

        using UnityWebRequest request = BuildJsonPostRequest($"{_baseUrl}/api/auth/signup", json);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            UserResponse user = JsonUtility.FromJson<UserResponse>(request.downloadHandler.text);
            UserAccount created = new UserAccount
            {
                Id = user.id,
                Username = user.email,
                Password = password
            };

            Debug.Log($"{LogPrefix} 회원가입 성공. email={created.Username}, userId={created.Id}");
            onCompleted?.Invoke(new AuthResult(AuthResultCode.Success, "회원가입 성공", created));
            yield break;
        }

        Debug.LogError($"{LogPrefix} 회원가입 실패. code={request.responseCode}, body={request.downloadHandler.text}");
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

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            onCompleted?.Invoke(new AuthResult(AuthResultCode.InvalidInput, "이메일/비밀번호는 공백일 수 없습니다."));
            yield break;
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        if (!normalizedEmail.Contains("@"))
        {
            onCompleted?.Invoke(new AuthResult(AuthResultCode.InvalidInput, "이메일 형식이 올바르지 않습니다. (@ 포함 필수)"));
            yield break;
        }

        LoginRequest requestDto = new LoginRequest { Email = normalizedEmail, Password = password };
        string json = JsonUtility.ToJson(requestDto);

        using UnityWebRequest request = BuildJsonPostRequest($"{_baseUrl}/api/auth/login", json);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
            UserAccount account = new UserAccount
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

        Debug.LogError($"{LogPrefix} 로그인 실패. code={request.responseCode}, body={request.downloadHandler.text}");
        onCompleted?.Invoke(MapFailure((int)request.responseCode, request.error, request.downloadHandler.text));
    }

    private static UnityWebRequest BuildJsonPostRequest(string url, string json)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(bodyRaw),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private static AuthResult MapFailure(int statusCode, string error, string responseText)
    {
        return statusCode switch
        {
            400 => new AuthResult(AuthResultCode.InvalidInput, "입력값이 올바르지 않습니다."),
            404 => new AuthResult(AuthResultCode.UserNotFound, "계정이 존재하지 않습니다."),
            401 => new AuthResult(AuthResultCode.WrongPassword, "비밀번호가 일치하지 않습니다."),
            409 => new AuthResult(AuthResultCode.DuplicateUsername, "이미 존재하는 아이디입니다."),
            _ => new AuthResult(AuthResultCode.DatabaseError, $"DB/API 오류: {responseText} {error}")
        };
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
