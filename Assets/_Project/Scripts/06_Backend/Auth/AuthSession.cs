using System;

public static class AuthSession
{
    private const string LogPrefix = "[06_Backend][AuthSession]";

    public static bool IsLoggedIn { get; private set; }
    public static string CurrentUserId { get; private set; } = string.Empty;
    public static string CurrentUsername { get; private set; } = string.Empty;

    public static event Action<bool> AuthStateChanged;

    public static void SetAuthenticated(UserAccount user)
    {
        IsLoggedIn = true;
        CurrentUserId = user.Id;
        CurrentUsername = user.Username;

        UnityEngine.Debug.Log($"{LogPrefix} 로그인 세션 갱신. userId={CurrentUserId}, username={CurrentUsername}");
        AuthStateChanged?.Invoke(true);
    }

    public static void Clear()
    {
        IsLoggedIn = false;
        CurrentUserId = string.Empty;
        CurrentUsername = string.Empty;

        UnityEngine.Debug.Log($"{LogPrefix} 로그인 세션 초기화.");
        AuthStateChanged?.Invoke(false);
    }
}
