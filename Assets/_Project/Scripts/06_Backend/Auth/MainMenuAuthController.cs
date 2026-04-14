using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuAuthController : MonoBehaviour
{
    private const string LogPrefix = "[06_Backend][MainMenuAuthController]";

    [Header("API")]
    [SerializeField] private string authApiBaseUrl = "http://localhost:8080";

    [Header("Signup UI")]
    [SerializeField] private TMP_InputField signupEmailInput;
    [SerializeField] private TMP_InputField signupNicknameInput;
    [SerializeField] private TMP_InputField signupPasswordInput;
    [SerializeField] private TMP_InputField signupConfirmPasswordInput;

    [Header("Login UI")]
    [SerializeField] private TMP_InputField loginEmailInput;
    [SerializeField] private TMP_InputField loginPasswordInput;

    [Header("Buttons")]
    [SerializeField] private Button signupButton;
    [SerializeField] private Button loginButton;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    private AuthService _authService;

    private void Awake()
    {
        AutoBindIfNeeded();

        string normalizedApiBaseUrl = NormalizeApiBaseUrl(authApiBaseUrl);
        _authService = new AuthService(normalizedApiBaseUrl);

        if (signupButton != null)
        {
            signupButton.onClick.RemoveListener(OnSignupClicked);
            signupButton.onClick.AddListener(OnSignupClicked);
            Debug.Log($"{LogPrefix} signupButton 리스너 연결 완료. object={signupButton.gameObject.name}");
        }
        else
        {
            Debug.LogError($"{LogPrefix} signupButton 참조가 없습니다. Inspector 연결 또는 버튼 이름 확인 필요.");
        }

        if (loginButton != null)
        {
            loginButton.onClick.RemoveListener(OnLoginClicked);
            loginButton.onClick.AddListener(OnLoginClicked);
            Debug.Log($"{LogPrefix} loginButton 리스너 연결 완료. object={loginButton.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} loginButton 참조가 없습니다.");
        }

        Debug.Log($"{LogPrefix} 초기화 완료. api={normalizedApiBaseUrl}");
        StartCoroutine(_authService.PingDatabase(result =>
        {
            SetStatus(result.IsSuccess
                ? "서버/DB 연결 확인 완료"
                : $"서버/DB 연결 실패: {BuildUserMessage(result)}");
        }));
    }

    private void OnDestroy()
    {
        if (signupButton != null)
            signupButton.onClick.RemoveListener(OnSignupClicked);

        if (loginButton != null)
            loginButton.onClick.RemoveListener(OnLoginClicked);
    }

    private void AutoBindIfNeeded()
    {
        signupButton ??= FindButtonByCandidates("Signup Button", "Signup", "SignUp", "회원가입", "Join");
        loginButton ??= FindButtonByCandidates("Login Button", "Login", "로그인");

        signupEmailInput ??= FindInputByCandidates("Signup Email Input", "SignupEmail", "Email");
        signupNicknameInput ??= FindInputByCandidates("Signup Nickname Input", "SignupNickname", "Nickname");
        signupPasswordInput ??= FindInputByCandidates("Signup Password Input", "SignupPassword", "Password");
        signupConfirmPasswordInput ??= FindInputByCandidates("Signup Confirm Password Input", "SignupConfirmPassword", "ConfirmPassword");

        loginEmailInput ??= FindInputByCandidates("Login Email Input", "LoginEmail");
        loginPasswordInput ??= FindInputByCandidates("Login Password Input", "LoginPassword");
        statusText ??= FindTextByCandidates("Status Text", "StatusText", "AuthStatus", "Status");
    }

    private static Button FindButtonByCandidates(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            GameObject found = GameObject.Find(candidate);
            if (found != null && found.TryGetComponent(out Button button))
                return button;
        }

        return null;
    }

    private static TMP_InputField FindInputByCandidates(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            GameObject found = GameObject.Find(candidate);
            if (found != null && found.TryGetComponent(out TMP_InputField input))
                return input;
        }

        return null;
    }

    private static TMP_Text FindTextByCandidates(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            GameObject found = GameObject.Find(candidate);
            if (found != null && found.TryGetComponent(out TMP_Text text))
                return text;
        }

        return null;
    }

    private void OnSignupClicked()
    {
        string email = signupEmailInput != null ? signupEmailInput.text.Trim() : string.Empty;
        string nickname = signupNicknameInput != null ? signupNicknameInput.text.Trim() : string.Empty;
        string password = signupPasswordInput != null ? signupPasswordInput.text : string.Empty;
        string confirmPassword = signupConfirmPasswordInput != null ? signupConfirmPasswordInput.text : string.Empty;

        StartCoroutine(_authService.Signup(email, nickname, password, confirmPassword, result =>
        {
            SetStatus(BuildUserMessage(result));

            if (!result.IsSuccess)
                return;

            Debug.Log($"{LogPrefix} 회원가입 성공 처리 완료. email={email}");
            if (loginEmailInput != null)
                loginEmailInput.text = email;

            if (loginPasswordInput != null)
                loginPasswordInput.text = password;
        }));
    }

    private void OnLoginClicked()
    {
        string email = loginEmailInput != null ? loginEmailInput.text.Trim() : string.Empty;
        string password = loginPasswordInput != null ? loginPasswordInput.text : string.Empty;

        StartCoroutine(_authService.Login(email, password, result =>
        {
            SetStatus(BuildUserMessage(result));
            if (result.IsSuccess)
                Debug.Log($"{LogPrefix} 로그인 성공 처리 완료. RoomLauncher 진입 가능 상태.");
        }));
    }

    private static string BuildUserMessage(AuthResult result)
    {
        return result.Code switch
        {
            AuthResultCode.Success => result.Message,
            AuthResultCode.InvalidInput => $"입력 오류: {result.Message}",
            AuthResultCode.DuplicateUsername => "회원가입 실패: 이미 존재하는 이메일입니다.",
            AuthResultCode.UserNotFound => "로그인 실패: 계정이 없습니다.",
            AuthResultCode.WrongPassword => "로그인 실패: 비밀번호가 일치하지 않습니다.",
            AuthResultCode.DatabaseError => "DB/API 오류가 발생했습니다. 콘솔 로그를 확인하세요.",
            _ => result.Message
        };
    }

    private void SetStatus(string message)
    {
        Debug.Log($"{LogPrefix} 상태 메시지 갱신. message={message}");

        if (statusText != null)
            statusText.text = message;
    }

    private static string NormalizeApiBaseUrl(string baseUrl)
    {
        string trimmed = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:8080" : baseUrl.Trim();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (trimmed.Contains("localhost"))
        {
            trimmed = trimmed.Replace("localhost", "10.0.2.2");
            Debug.LogWarning($"{LogPrefix} Android 빌드에서 localhost 대신 10.0.2.2 사용: {trimmed}");
        }
#endif
        return trimmed;
    }
}
