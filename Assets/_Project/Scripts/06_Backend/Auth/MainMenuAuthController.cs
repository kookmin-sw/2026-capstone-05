using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class MainMenuAuthController : MonoBehaviour
{
    private const string LogPrefix = "[06_Backend][MainMenuAuthController]";

    [Header("API")]
    [SerializeField] private string authApiBaseUrl = "http://localhost:8080";
    [SerializeField] private bool allowRuntimeApiBaseUrlOverride = true;
    [SerializeField] private string authApiBaseUrlConfigFileName = "auth-api-base-url.txt";

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

    [Header("Post Login Fallback UI")]
    [SerializeField] private GameObject loginMenuRoot;
    [SerializeField] private GameObject mainMenuRoot;

    [Header("Flow")]
    [SerializeField] private bool replaceLoginButtonPersistentOnClick = true;
    [Tooltip("로그인 성공 시 Inspector에서 연결한 패널 전환/애니메이션 함수를 호출합니다.")]
    [SerializeField] private UnityEvent onLoginSuccess;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Color normalStatusColor = Color.white;
    [SerializeField] private Color successStatusColor = new(0.6f, 1f, 0.6f);
    [SerializeField] private Color errorStatusColor = new(1f, 0.6f, 0.6f);

    private AuthService _authService;
    private bool _isSubmitting;

    private void Awake()
    {
        AuthSession.Clear();
        AutoBindIfNeeded();

        string normalizedApiBaseUrl = NormalizeApiBaseUrl(ResolveApiBaseUrl(authApiBaseUrl, allowRuntimeApiBaseUrlOverride, authApiBaseUrlConfigFileName));
        _authService = new AuthService(normalizedApiBaseUrl);

        if (signupButton != null)
        {
            signupButton.onClick.RemoveListener(OnSignupClicked);
            signupButton.onClick.AddListener(OnSignupClicked);
            EnsureButtonFeedback(signupButton);
            Debug.Log($"{LogPrefix} signupButton 리스너 연결 완료. object={signupButton.gameObject.name}");
        }
        else
        {
            Debug.LogError($"{LogPrefix} signupButton 참조가 없습니다. Inspector 연결 또는 버튼 이름 확인 필요.");
        }

        if (loginButton != null)
        {
            if (replaceLoginButtonPersistentOnClick)
                loginButton.onClick = new Button.ButtonClickedEvent();

            loginButton.onClick.RemoveListener(OnLoginClicked);
            loginButton.onClick.AddListener(OnLoginClicked);
            EnsureButtonFeedback(loginButton);
            Debug.Log($"{LogPrefix} loginButton 리스너 연결 완료. object={loginButton.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} loginButton 참조가 없습니다.");
        }

        int onLoginSuccessCount = onLoginSuccess != null ? onLoginSuccess.GetPersistentEventCount() : 0;
        bool hasValidPersistentListener = HasValidPersistentListener(onLoginSuccess);
        Debug.Log($"{LogPrefix} 로그인 성공 이벤트 슬롯 수={onLoginSuccessCount}, 유효 리스너={hasValidPersistentListener}, fallbackLoginMenu={loginMenuRoot != null}, fallbackMainMenu={mainMenuRoot != null}");

        SetStatus("서버 연결 확인 중...", normalStatusColor);
        StartCoroutine(_authService.PingDatabase(result =>
        {
            SetStatus(result.IsSuccess
                ? "서버/DB 연결 확인 완료"
                : $"서버/DB 연결 실패: {BuildUserMessage(result)}", result.IsSuccess ? successStatusColor : errorStatusColor);
        }));

        Debug.Log($"{LogPrefix} 초기화 완료. api={normalizedApiBaseUrl}");
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
        signupEmailInput ??= FindInputByCandidates("Signup Email Input", "SignupEmail", "Email", "ID InputField");
        signupNicknameInput ??= FindInputByCandidates("Signup Nickname Input", "SignupNickname", "Nickname", "Name InputField");
        signupPasswordInput ??= FindInputByCandidates("Signup Password Input", "SignupPassword", "Password", "PW InputField");
        signupConfirmPasswordInput ??= FindInputByCandidates("Signup Confirm Password Input", "SignupConfirmPassword", "ConfirmPassword", "PW Confirm InputField");

        loginEmailInput ??= FindInputByCandidates("Login Email Input", "LoginEmail", "ID InputField");
        loginPasswordInput ??= FindInputByCandidates("Login Password Input", "LoginPassword", "PW InputField");

        signupButton = ResolveSignupSubmitButton(signupButton, signupEmailInput, signupNicknameInput, signupPasswordInput, signupConfirmPasswordInput);
        loginButton ??= FindButtonByCandidates("Login Button", "Login", "로그인");

        statusText ??= FindTextByCandidates("Status Text", "AuthStatus", "Status", "StatusText", "Notice Text");
        loginMenuRoot ??= FindObjectByCandidates("Login Menu", "LoginMenu");
        mainMenuRoot ??= FindObjectByCandidates("Main Menu", "MainMenu");
    }

    private static Button ResolveSignupSubmitButton(Button configuredButton, params TMP_InputField[] signupInputs)
    {
        Transform signupFormRoot = FindNearestCommonAncestor(signupInputs);
        if (signupFormRoot != null)
        {
            Button formButton = FindButtonUnder(
                signupFormRoot,
                "Apply Button",
                "Submit Button",
                "Confirm Button",
                "Signup Submit",
                "SignUp Submit",
                "Create Account",
                "가입 완료",
                "회원가입 완료",
                "가입하기",
                "Registration Button",
                "Signup Button",
                "SignUp Button",
                "Signup",
                "SignUp",
                "회원가입");

            if (formButton != null)
            {
                if (configuredButton != null && configuredButton != formButton)
                {
                    Debug.LogWarning($"{LogPrefix} signupButton reference pointed outside the signup form. Runtime binding changed from '{configuredButton.gameObject.name}' to '{formButton.gameObject.name}'.");
                }

                return formButton;
            }
        }

        return configuredButton ?? FindButtonByCandidates("Registration Button", "Signup Button", "SignUp Button", "Signup", "SignUp", "회원가입");
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

    private static Button FindButtonUnder(Transform root, params string[] candidates)
    {
        if (root == null)
            return null;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            if (ContainsAny(button.gameObject.name, candidates))
                return button;

            TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null && ContainsAny(tmpLabel.text, candidates))
                return button;

            Text legacyLabel = button.GetComponentInChildren<Text>(true);
            if (legacyLabel != null && ContainsAny(legacyLabel.text, candidates))
                return button;
        }

        return null;
    }

    private static Transform FindNearestCommonAncestor(params TMP_InputField[] inputs)
    {
        TMP_InputField first = null;
        foreach (TMP_InputField input in inputs)
        {
            if (input != null)
            {
                first = input;
                break;
            }
        }

        if (first == null)
            return null;

        Transform candidate = first.transform;
        while (candidate != null)
        {
            bool containsAll = true;
            foreach (TMP_InputField input in inputs)
            {
                if (input == null)
                    continue;

                if (!IsChildOfOrSelf(input.transform, candidate))
                {
                    containsAll = false;
                    break;
                }
            }

            if (containsAll)
                return candidate;

            candidate = candidate.parent;
        }

        return null;
    }

    private static bool IsChildOfOrSelf(Transform child, Transform parent)
    {
        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static bool ContainsAny(string source, params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (source.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
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

    private static GameObject FindObjectByCandidates(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            GameObject found = GameObject.Find(candidate);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void EnsureButtonFeedback(Button button)
    {
        if (button == null || button.GetComponent<AuthButtonFeedback>() != null)
            return;

        button.gameObject.AddComponent<AuthButtonFeedback>();
    }

    private void OnSignupClicked()
    {
        if (_isSubmitting)
            return;

        string email = signupEmailInput != null ? signupEmailInput.text.Trim() : string.Empty;
        string nickname = signupNicknameInput != null ? signupNicknameInput.text.Trim() : string.Empty;
        string password = signupPasswordInput != null ? signupPasswordInput.text : string.Empty;
        string confirmPassword = signupConfirmPasswordInput != null ? signupConfirmPasswordInput.text : string.Empty;

        SetBusy(true);
        SetStatus("회원가입 요청 중...", normalStatusColor);

        StartCoroutine(_authService.Signup(email, nickname, password, confirmPassword, result =>
        {
            SetBusy(false);
            SetStatus(BuildUserMessage(result), result.IsSuccess ? successStatusColor : errorStatusColor);

            if (!result.IsSuccess)
                return;

            Debug.Log($"{LogPrefix} 회원가입 성공 처리 완료. email={email}");

            if (loginEmailInput != null)
                loginEmailInput.text = email;

            if (loginPasswordInput != null)
                loginPasswordInput.text = password;

            if (signupPasswordInput != null)
                signupPasswordInput.text = string.Empty;

            if (signupConfirmPasswordInput != null)
                signupConfirmPasswordInput.text = string.Empty;
        }));
    }

    private void OnLoginClicked()
    {
        if (_isSubmitting)
            return;

        string email = loginEmailInput != null ? loginEmailInput.text.Trim() : string.Empty;
        string password = loginPasswordInput != null ? loginPasswordInput.text : string.Empty;

        SetBusy(true);
        SetStatus("로그인 요청 중...", normalStatusColor);

        StartCoroutine(_authService.Login(email, password, result =>
        {
            SetBusy(false);
            SetStatus(BuildUserMessage(result), result.IsSuccess ? successStatusColor : errorStatusColor);

            if (!result.IsSuccess)
                return;

            Debug.Log($"{LogPrefix} 로그인 성공 처리 완료. 후속 이벤트 실행.");
            ExecutePostLoginFlow();
        }));
    }

    private void ExecutePostLoginFlow()
    {
        if (HasValidPersistentListener(onLoginSuccess))
        {
            onLoginSuccess?.Invoke();
            return;
        }

        bool usedFallback = false;

        if (loginMenuRoot != null)
        {
            loginMenuRoot.SetActive(false);
            usedFallback = true;
        }

        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(true);
            usedFallback = true;
        }

        if (usedFallback)
        {
            Debug.LogWarning($"{LogPrefix} onLoginSuccess 함수가 없어 fallback 패널 전환 실행(loginMenu off / mainMenu on).");
            return;
        }

        Debug.LogError($"{LogPrefix} 로그인 성공 후 전환할 동작이 없습니다. onLoginSuccess 함수 또는 fallback 패널 참조를 설정하세요.");
    }

    private static bool HasValidPersistentListener(UnityEvent unityEvent)
    {
        if (unityEvent == null)
            return false;

        int count = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            UnityEngine.Object target = unityEvent.GetPersistentTarget(i);
            string methodName = unityEvent.GetPersistentMethodName(i);
            if (target != null && !string.IsNullOrWhiteSpace(methodName))
                return true;
        }

        return false;
    }

    private void SetBusy(bool isBusy)
    {
        _isSubmitting = isBusy;
        if (signupButton != null)
            signupButton.interactable = !isBusy;

        if (loginButton != null)
            loginButton.interactable = !isBusy;
    }

    private static string BuildUserMessage(AuthResult result)
    {
        return result.Code switch
        {
            AuthResultCode.Success => result.Message,
            AuthResultCode.InvalidInput => $"입력 오류: {result.Message}",
            AuthResultCode.DuplicateUsername => $"회원가입 실패: {result.Message}",
            AuthResultCode.DuplicateNickname => $"회원가입 실패: {result.Message}",
            AuthResultCode.UserNotFound => $"로그인 실패: {result.Message}",
            AuthResultCode.WrongPassword => $"로그인 실패: {result.Message}",
            AuthResultCode.NetworkError => $"네트워크 오류: {result.Message}",
            AuthResultCode.DatabaseError => $"서버 오류: {result.Message}",
            _ => result.Message
        };
    }

    private void SetStatus(string message, Color color)
    {
        Debug.Log($"{LogPrefix} 상태 메시지 갱신. message={message}");

        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }

    private static string NormalizeApiBaseUrl(string baseUrl)
    {
        string trimmed = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:8080" : baseUrl.Trim();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsLoopbackApiBaseUrl(trimmed))
        {
            trimmed = ReplaceUriHost(trimmed, "10.0.2.2");
            Debug.LogWarning($"{LogPrefix} Android 빌드에서 localhost 대신 10.0.2.2 사용: {trimmed}");
        }
#endif
#if !UNITY_EDITOR
        if (IsLoopbackApiBaseUrl(trimmed))
        {
            Debug.LogWarning($"{LogPrefix} Auth API base URL points to this client device ({trimmed}). For remote multiplayer, set --auth-api-base-url, NUNBORA_AUTH_API_BASE_URL, or auth-api-base-url.txt to the Docker host IP/public tunnel URL.");
        }
#endif
        return trimmed;
    }

    private static string ResolveApiBaseUrl(string configuredBaseUrl, bool allowRuntimeOverride, string configFileName)
    {
        if (!allowRuntimeOverride)
            return configuredBaseUrl;

        string commandLineOverride = GetCommandLineValue("--auth-api-base-url");
        if (!string.IsNullOrWhiteSpace(commandLineOverride))
            return commandLineOverride;

        string environmentOverride = Environment.GetEnvironmentVariable("NUNBORA_AUTH_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(environmentOverride))
            return environmentOverride;

        string fileOverride = TryReadApiBaseUrlConfig(configFileName);
        if (!string.IsNullOrWhiteSpace(fileOverride))
            return fileOverride;

        return configuredBaseUrl;
    }

    private static string TryReadApiBaseUrlConfig(string configFileName)
    {
        if (string.IsNullOrWhiteSpace(configFileName))
            return null;

        foreach (string path in GetApiBaseUrlConfigPaths(configFileName.Trim()))
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string value = line.Trim();
                    if (value.Length == 0 || value.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    Debug.Log($"{LogPrefix} Auth API base URL loaded from config file: {path}");
                    return value;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Failed to read Auth API base URL config file: {path}, error={ex.Message}");
            }
        }

        return null;
    }

    private static string[] GetApiBaseUrlConfigPaths(string configFileName)
    {
        if (Path.IsPathRooted(configFileName))
            return new[] { configFileName };

        return new[]
        {
            Path.Combine(Application.persistentDataPath, configFileName),
            Path.Combine(Application.streamingAssetsPath, configFileName),
            Path.Combine(GetPlayerRootDirectory(), configFileName),
            Path.Combine(Directory.GetCurrentDirectory(), configFileName)
        };
    }

    private static string GetPlayerRootDirectory()
    {
        string dataPath = Application.dataPath;
        if (string.IsNullOrWhiteSpace(dataPath))
            return AppDomain.CurrentDomain.BaseDirectory;

        DirectoryInfo dataDirectory = new(dataPath);
        if (dataDirectory.Name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase) && dataDirectory.Parent != null)
            return dataDirectory.Parent.FullName;

        return dataDirectory.Parent?.FullName ?? dataDirectory.FullName;
    }

    private static bool IsLoopbackApiBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri))
            return false;

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceUriHost(string baseUrl, string host)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri))
            return baseUrl;

        UriBuilder builder = new(uri)
        {
            Host = host
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string GetCommandLineValue(string optionName)
    {
        string[] args = Environment.GetCommandLineArgs();
        string prefix = optionName + "=";

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return arg.Substring(prefix.Length);

            if (string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }

        return null;
    }
}
