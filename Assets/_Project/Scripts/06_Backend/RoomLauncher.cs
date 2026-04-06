using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    private const string LogInput = "[Input]";
    private enum MenuStep
    {
        Login,
        Start,
        RoleSelect,
        JoinCode
    }

    private static RoomLauncher _instance;

    [Header("Scene Flow")]
    [SerializeField] private string menuSceneName = "Main_menu";
    [SerializeField] private string gameScenePath = "Assets/_Project/Scenes/06_Backend/TestMain.unity";
    [SerializeField] private string gameSceneNameFallback = "TestMain";

    [Header("Network")]
    [SerializeField] private int maxPlayers = 8;
    [SerializeField] private int hostCodeRetryCount = 15;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private PlayerSpawner _playerSpawner;
    private bool _callbacksRegistered;
    private bool _isConnecting;

    private MenuStep _menuStep = MenuStep.Login;
    private string _joinCodeInput = string.Empty;
    private string _sessionCode = string.Empty;
    private string _status = "로그인 후 시작하기를 눌러주세요.";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (!IsMenuScene(SceneManager.GetActiveScene().name))
        {
            TryLoadMenuScene();
        }
    }

    private Button _loginButton;
    private Button _startButton;
    private Button _hostButton;
    private Button _entryButton;
    private Button _backButton;
    private TMP_InputField _roomCodeInputField;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindMenuButtons();
    }

    private void Start()
    {
        BindMenuButtonsIfNeeded(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindMenuButtonsIfNeeded(scene.name);
    }

    private void BindMenuButtonsIfNeeded(string sceneName)
    {
        if (!IsMenuScene(sceneName))
            return;

        EnsureMenuCursor();
        UnbindMenuButtons();

        _loginButton = FindButtonByLabel("Login Button", "로그인");
        _startButton = FindButtonByLabel("Start Button", "시작");
        _hostButton = FindButtonByLabel("Host Button", "호스트");
        _entryButton = FindButtonByLabel("Entry Button", "참가", "Join");
        _backButton = FindButtonByLabel("back Button", "Back", "뒤로");
        _roomCodeInputField = FindRoomCodeInputField();

        if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginButtonClicked);
        if (_startButton != null) _startButton.onClick.AddListener(OnStartButtonClicked);
        if (_hostButton != null) _hostButton.onClick.AddListener(OnHostButtonClicked);
        if (_entryButton != null) _entryButton.onClick.AddListener(OnEntryButtonClicked);
        if (_backButton != null) _backButton.onClick.AddListener(OnBackButtonClicked);

        UpdateMenuInteractivity();
    }

    private void UnbindMenuButtons()
    {
        if (_loginButton != null) _loginButton.onClick.RemoveListener(OnLoginButtonClicked);
        if (_startButton != null) _startButton.onClick.RemoveListener(OnStartButtonClicked);
        if (_hostButton != null) _hostButton.onClick.RemoveListener(OnHostButtonClicked);
        if (_entryButton != null) _entryButton.onClick.RemoveListener(OnEntryButtonClicked);
        if (_backButton != null) _backButton.onClick.RemoveListener(OnBackButtonClicked);
    }

    public void OnLoginButtonClicked()
    {
        if (_isConnecting)
            return;

        _menuStep = MenuStep.Start;
        _status = "로그인 완료. 시작하기를 눌러주세요.";
    }

    public void OnStartButtonClicked()
    {
        if (_isConnecting)
            return;

        _menuStep = MenuStep.RoleSelect;
        _status = "호스트 / 참가를 선택하세요.";
    }

    public void OnHostButtonClicked()
    {
        if (_isConnecting)
            return;

        _ = StartAsHostWithCodeAsync();
    }

    public void OnEntryButtonClicked()
    {
        if (_isConnecting)
            return;

        string normalized = NormalizeCode(_roomCodeInputField != null ? _roomCodeInputField.text : _joinCodeInput);
        if (string.IsNullOrEmpty(normalized))
        {
            _menuStep = MenuStep.JoinCode;
            _status = "유효한 방 코드를 입력하세요. (1~1000)";
            return;
        }

        _joinCodeInput = normalized;
        _ = StartGameAsync(normalized, GameMode.Client);
    }

    public void OnBackButtonClicked()
    {
        if (_isConnecting)
            return;

        _menuStep = MenuStep.RoleSelect;
        _status = "호스트 / 참가를 선택하세요.";
    }


    private bool IsMenuScene(string sceneName)
    {
        return string.Equals(sceneName, menuSceneName, StringComparison.Ordinal);
    }

    private void TryLoadMenuScene()
    {
        int buildIndex = ResolveSceneBuildIndexByName(menuSceneName);
        if (buildIndex >= 0)
        {
            SceneManager.LoadScene(buildIndex);
        }
    }

    private static Button FindButtonByLabel(params string[] labels)
    {
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                continue;

            string text = label.text?.Trim();
            if (string.IsNullOrEmpty(text))
                continue;

            for (int j = 0; j < labels.Length; j++)
            {
                if (string.Equals(text, labels[j], StringComparison.OrdinalIgnoreCase))
                    return button;
            }
        }

        return null;
    }

    private static TMP_InputField FindRoomCodeInputField()
    {
        TMP_InputField[] inputFields = UnityEngine.Object.FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < inputFields.Length; i++)
        {
            TMP_InputField input = inputFields[i];
            if (string.Equals(input.gameObject.name, "Room Number InputField", StringComparison.Ordinal))
                return input;
        }

        return null;
    }

    private void UpdateMenuInteractivity()
    {
        bool interactable = !_isConnecting;
        if (_loginButton != null) _loginButton.interactable = interactable;
        if (_startButton != null) _startButton.interactable = interactable;
        if (_hostButton != null) _hostButton.interactable = interactable;
        if (_entryButton != null) _entryButton.interactable = interactable;
        if (_backButton != null) _backButton.interactable = interactable;
        if (_roomCodeInputField != null) _roomCodeInputField.interactable = interactable;
    }

    private static void EnsureMenuCursor()
    {
        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;

        if (!Cursor.visible)
            Cursor.visible = true;
    }

    private async Task StartAsHostWithCodeAsync()
    {
        if (_isConnecting)
            return;

        for (int i = 0; i < Mathf.Max(1, hostCodeRetryCount); i++)
        {
            string candidateCode = GenerateHostRoomCode();
            Debug.Log($"[RoomLauncher] Host code generation attempt {i + 1}: {candidateCode}");

            bool ok = await StartGameAsync(candidateCode, GameMode.Host, suppressFailureStatus: i < hostCodeRetryCount - 1);
            if (ok)
            {
                Debug.Log($"[RoomLauncher] Host start success with code: {candidateCode}");
                return;
            }
        }

        _status = "중복 없는 방 코드 생성에 실패했습니다. 다시 시도해주세요.";
        Debug.LogWarning("[RoomLauncher] Host start failed after all retry attempts.");
    }

    private async Task<bool> StartGameAsync(string roomCode, GameMode mode, bool suppressFailureStatus = false)
    {
        _isConnecting = true;
        UpdateMenuInteractivity();
        _status = $"연결 중... ({roomCode})";

        try
        {
            EnsureRunnerReady();

            if (_runner.IsRunning)
                await _runner.Shutdown();

            _spawnedPlayers.Clear();
            _playerSpawner = new PlayerSpawner(_spawnedPlayers, maxPlayers);

            int sceneBuildIndex = ResolveGameSceneBuildIndex();
            if (sceneBuildIndex < 0)
                throw new Exception($"Build Settings에서 '{gameSceneNameFallback}' 씬을 찾지 못했습니다.");

            StartGameResult result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = roomCode,
                PlayerCount = maxPlayers,
                Scene = SceneRef.FromIndex(sceneBuildIndex),
                SceneManager = _sceneManager
            });

            if (result.Ok)
            {
                _sessionCode = roomCode;
                _status = mode == GameMode.Host
                    ? $"호스트 시작 완료 (code : {roomCode})"
                    : $"참가 완료 (code : {roomCode})";
                Debug.Log($"[RoomLauncher] StartGame success. Mode={mode}, Code={roomCode}");
                return true;
            }

            if (!suppressFailureStatus)
                _status = $"시작 실패: {result.ShutdownReason}";

            Debug.LogWarning($"[RoomLauncher] StartGame failed. Mode={mode}, Code={roomCode}, Reason={result.ShutdownReason}");
            return false;
        }
        catch (Exception ex)
        {
            if (!suppressFailureStatus)
                _status = $"오류: {ex.Message}";

            Debug.LogError($"[RoomLauncher] StartGame exception. Mode={mode}, Code={roomCode}, Error={ex}");
            return false;
        }
        finally
        {
            _isConnecting = false;
            UpdateMenuInteractivity();
        }
    }

    private void EnsureRunnerReady()
    {
        if (_runner == null)
        {
            _runner = GetComponent<NetworkRunner>();
            if (_runner == null)
                _runner = gameObject.AddComponent<NetworkRunner>();
        }

        _runner.ProvideInput = true;
        Debug.Log($"{LogInput} Runner ready. ProvideInput=true, LocalPlayer={_runner.LocalPlayer}");

        if (!_callbacksRegistered)
        {
            _runner.AddCallbacks(this);
            _callbacksRegistered = true;
        }

        if (_sceneManager == null)
        {
            _sceneManager = GetComponent<NetworkSceneManagerDefault>();
            if (_sceneManager == null)
                _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
    }

    private int ResolveGameSceneBuildIndex()
    {
        int byPath = SceneUtility.GetBuildIndexByScenePath(gameScenePath);
        if (byPath >= 0)
            return byPath;

        return ResolveSceneBuildIndexByName(gameSceneNameFallback);
    }

    private static int ResolveSceneBuildIndexByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return -1;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string candidate = Path.GetFileNameWithoutExtension(scenePath);
            if (string.Equals(candidate, sceneName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string NormalizeCode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string digitsOnly = string.Empty;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsDigit(c))
                digitsOnly += c;
        }

        if (digitsOnly.Length == 0)
            return string.Empty;

        if (!int.TryParse(digitsOnly, out int value))
            return string.Empty;

        if (value < 1 || value > 1000)
            return string.Empty;

        return value.ToString();
    }

    private static string GenerateHostRoomCode()
    {
        int value = RandomNumberGenerator.GetInt32(1, 1001);
        return value.ToString();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.Object.FindFirstObjectByType<RoomLauncher>() != null)
            return;

        GameObject go = new(nameof(RoomLauncher));
        go.AddComponent<RoomLauncher>();
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        _status = "서버 연결 완료";
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        _status = $"연결 종료: {reason}";
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        _status = $"연결 실패: {reason}";
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        if (_playerSpawner == null)
            _playerSpawner = new PlayerSpawner(_spawnedPlayers, maxPlayers);

        bool spawned = _playerSpawner.TrySpawn(runner, player);
        if (!spawned)
        {
            _status = "Player prefab 스폰 실패. PrefabTable 설정을 확인하세요.";
            return;
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        _playerSpawner?.Despawn(runner, player);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        _spawnedPlayers.Clear();
        _status = $"세션 종료: {shutdownReason}";
    }

    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (!BackendLocalInputRegistry.TryCollect(runner, out BackendNetworkInputData data))
            return;

        input.Set(data);

        if (data.Move.sqrMagnitude > 0.0001f || data.Look.sqrMagnitude > 0.0001f || data.JumpPressed)
        {
            Debug.Log($"{LogInput} Input sent to server. LocalPlayer={runner.LocalPlayer}, Move={data.Move}, Look={data.Look}, Jump={data.JumpPressed}, Sprint={data.SprintPressed}");
        }
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
