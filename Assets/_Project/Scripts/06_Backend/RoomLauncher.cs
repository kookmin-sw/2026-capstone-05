using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class RoomLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    private const string LogPrefix = "[06_Backend][RoomLauncher]";

    [Header("Scene")]
    [SerializeField] private string startSceneName = "Main_menu";
    [SerializeField] private string gameScenePath = "Assets/_Project/Scenes/06_Backend/TestMain.unity";
    [SerializeField] private string gameSceneNameFallback = "TestMain";

    [Header("Network")]
    [SerializeField] private int maxPlayers = 8;
    [SerializeField] private string roomSessionPrefix = "backend-room-";
    [SerializeField] private NetworkObject playerPrefab;

    private static RoomLauncher _instance;

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _callbacksRegistered;
    private bool _isConnecting;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();
    private readonly Dictionary<NetworkId, bool> _configuredLocalStates = new();

    private Button _loginButton;
    private Button _startButton;
    private Button _hostButton;
    private Button _joinButton;
    private TMP_InputField _roomCodeInput;

    private string _latestRoomCode = string.Empty;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"{LogPrefix} Awake 완료. sceneLoaded 콜백 등록.");
    }

    private void Start()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Debug.Log($"{LogPrefix} Start 진입. 현재 씬에서 즉시 메뉴 바인딩 시도. scene={activeScene.name}");
        if (TryBindMenuUi(activeScene.name))
        {
            UnlockCursorForMenu();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstanceExists("BeforeSceneLoad");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        EnsureInstanceExists("AfterSceneLoad");
    }

    private static void EnsureInstanceExists(string phase)
    {
        if (FindObjectOfType<RoomLauncher>() != null)
        {
            Debug.Log($"{LogPrefix} Bootstrap({phase}) 기존 RoomLauncher 발견.");
            return;
        }

        Debug.Log($"{LogPrefix} Bootstrap({phase}) RoomLauncher 생성.");
        GameObject launcherGo = new(nameof(RoomLauncher));
        launcherGo.AddComponent<RoomLauncher>();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (TryBindMenuUi(scene.name))
        {
            UnlockCursorForMenu();
        }
    }

    private static void UnlockCursorForMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private bool TryBindMenuUi(string sceneName)
    {
        _loginButton = FindButtonByCandidates("Login Button", "Login", "로그인");
        _startButton = FindButtonByCandidates("Start Button", "Start", "시작");
        _hostButton = FindButtonByCandidates("Host Button", "Host", "호스트", "방 만들기", "생성");
        _joinButton = FindButtonByCandidates("Entry Button", "Entry", "Join", "참가", "입장");
        _roomCodeInput = FindRoomCodeInputField();

        bool hasAnyMenuControl = _loginButton != null || _startButton != null || _hostButton != null || _joinButton != null || _roomCodeInput != null;
        if (!hasAnyMenuControl)
            return false;

        Debug.Log($"{LogPrefix} 메뉴 UI 바인딩 시작. scene={sceneName}, configuredStartScene={startSceneName}");

        if (_loginButton != null)
        {
            _loginButton.onClick.RemoveListener(OnLoginButtonClicked);
            _loginButton.onClick.AddListener(OnLoginButtonClicked);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} Login Button을 찾지 못했습니다.");
        }

        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(OnStartButtonClicked);
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} Start Button을 찾지 못했습니다.");
        }

        if (_hostButton != null)
        {
            _hostButton.onClick.RemoveListener(OnHostButtonClicked);
            _hostButton.onClick.AddListener(OnHostButtonClicked);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} Host Button을 찾지 못했습니다.");
        }

        if (_joinButton != null)
        {
            _joinButton.onClick.RemoveListener(OnJoinButtonClicked);
            _joinButton.onClick.AddListener(OnJoinButtonClicked);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} Entry Button(참가 버튼)을 찾지 못했습니다.");
        }

        if (_roomCodeInput == null)
        {
            Debug.LogWarning($"{LogPrefix} 방 코드 입력 InputField를 찾지 못했습니다. 참가 기능이 동작하지 않을 수 있습니다.");
        }

        return true;
    }

    private static Button FindButtonByCandidates(params string[] candidates)
    {
        Button[] allButtons = FindObjectsOfType<Button>(true);
        foreach (Button button in allButtons)
        {
            if (ContainsAny(button.name, candidates))
                return button;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && ContainsAny(label.text, candidates))
                return button;
        }

        return null;
    }

    private static bool ContainsAny(string source, IEnumerable<string> candidates)
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

    private static TMP_InputField FindRoomCodeInputField()
    {
        TMP_InputField[] inputFields = FindObjectsOfType<TMP_InputField>(true);
        foreach (TMP_InputField input in inputFields)
        {
            if (input.placeholder is TMP_Text placeholderText)
            {
                if (placeholderText.text.IndexOf("방 번호", StringComparison.Ordinal) >= 0 ||
                    placeholderText.text.IndexOf("방 코드를", StringComparison.Ordinal) >= 0 ||
                    placeholderText.text.IndexOf("방 코드", StringComparison.Ordinal) >= 0)
                {
                    return input;
                }
            }

            if (input.name.IndexOf("Room", StringComparison.OrdinalIgnoreCase) >= 0 ||
                input.name.IndexOf("Code", StringComparison.OrdinalIgnoreCase) >= 0 ||
                input.name.IndexOf("PW", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return input;
            }
        }

        return null;
    }

    private void OnLoginButtonClicked()
    {
        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 로그인 버튼 클릭.");
    }

    private void OnStartButtonClicked()
    {
        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 시작하기 버튼 클릭.");
    }

    private void OnHostButtonClicked()
    {
        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 호스트 버튼 클릭.");

        string roomCode = GenerateThreeDigitRoomCode();
        string sessionName = ResolveSessionName(roomCode);

        _latestRoomCode = roomCode;
        if (_roomCodeInput != null)
            _roomCodeInput.text = roomCode;

        Debug.Log($"{LogPrefix} 호스트 방 생성. 생성된 3자리 방 코드={roomCode}");
        Debug.Log($"{LogPrefix} 코드 -> 룸 이름 매핑. code={roomCode}, session={sessionName}");

        _ = StartGameAsync(roomCode, sessionName, GameMode.Host);
    }

    private void OnJoinButtonClicked()
    {
        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 참가 버튼 클릭.");

        string inputCode = _roomCodeInput != null ? _roomCodeInput.text.Trim() : string.Empty;
        Debug.Log($"{LogPrefix} 참가자가 입력한 코드={inputCode}");

        if (!IsValidRoomCode(inputCode))
        {
            Debug.LogWarning($"{LogPrefix} 참가 코드 유효성 실패. 반드시 3자리 숫자여야 합니다.");
            return;
        }

        string sessionName = ResolveSessionName(inputCode);
        Debug.Log($"{LogPrefix} 코드 -> 룸 이름 매핑. code={inputCode}, session={sessionName}");
        Debug.Log($"{LogPrefix} 코드 기반 방 접속 시도. code={inputCode}, session={sessionName}");

        _ = StartGameAsync(inputCode, sessionName, GameMode.Client);
    }

    private static string GenerateThreeDigitRoomCode()
    {
        int value = UnityEngine.Random.Range(100, 1000);
        return value.ToString("000");
    }

    private static bool IsValidRoomCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code) &&
               code.Length == 3 &&
               int.TryParse(code, out _);
    }

    private string ResolveSessionName(string roomCode)
    {
        return $"{roomSessionPrefix}{roomCode}";
    }

    private async Task StartGameAsync(string roomCode, string sessionName, GameMode mode)
    {
        if (_isConnecting)
        {
            Debug.LogWarning($"{LogPrefix} 이미 접속 시도 중입니다. code={roomCode}, mode={mode}");
            return;
        }

        _isConnecting = true;

        try
        {
            EnsureRunnerReady();

            if (_runner.IsRunning)
            {
                await _runner.Shutdown();
            }

            _spawnedPlayers.Clear();
            _configuredLocalStates.Clear();

            int gameSceneBuildIndex = ResolveGameSceneBuildIndex();
            if (gameSceneBuildIndex < 0)
            {
                throw new Exception($"게임 씬을 찾지 못했습니다. path={gameScenePath}, fallback={gameSceneNameFallback}");
            }

            StartGameResult result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = maxPlayers,
                Scene = SceneRef.FromIndex(gameSceneBuildIndex),
                SceneManager = _sceneManager
            });

            if (result.Ok)
            {
                Debug.Log($"{LogPrefix} 방 입장 성공. mode={mode}, code={roomCode}, session={sessionName}");
            }
            else
            {
                Debug.LogError($"{LogPrefix} 방 입장 실패. mode={mode}, code={roomCode}, session={sessionName}, reason={result.ShutdownReason}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LogPrefix} 방 입장 중 예외 발생. {ex}");
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private void EnsureRunnerReady()
    {
        if (_runner == null)
        {
            _runner = GetComponent<NetworkRunner>();
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
            }
        }

        if (_sceneManager == null)
        {
            _sceneManager = GetComponent<NetworkSceneManagerDefault>();
            if (_sceneManager == null)
            {
                _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            }
        }

        if (!_callbacksRegistered)
        {
            _runner.AddCallbacks(this);
            _callbacksRegistered = true;
        }
    }

    private int ResolveGameSceneBuildIndex()
    {
        int byPath = SceneUtility.GetBuildIndexByScenePath(gameScenePath);
        if (byPath >= 0)
            return byPath;

        string fallbackName = string.IsNullOrWhiteSpace(gameSceneNameFallback)
            ? Path.GetFileNameWithoutExtension(gameScenePath)
            : gameSceneNameFallback;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            if (string.Equals(sceneName, fallbackName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private void EnsurePlayerPrefabAssigned()
    {
        if (playerPrefab != null)
            return;

        if (NetworkProjectConfig.Global?.PrefabTable == null)
        {
            Debug.LogError($"{LogPrefix} NetworkProjectConfig.Global.PrefabTable이 비어 있습니다.");
            return;
        }

        foreach ((NetworkPrefabId _, INetworkPrefabSource source) in NetworkProjectConfig.Global.PrefabTable.GetEntries())
        {
            NetworkObject candidate = source switch
            {
                NetworkPrefabSourceStatic staticSource => staticSource.Object,
                NetworkPrefabSourceStaticLazy staticLazySource => staticLazySource.Object.asset,
                NetworkPrefabSourceResource resourceSource => TryResolveResourcePrefab(resourceSource),
                _ => null
            };

            if (candidate == null)
                continue;

            if (!string.Equals(candidate.name, "Player", StringComparison.OrdinalIgnoreCase))
                continue;

            playerPrefab = candidate;
            Debug.Log($"{LogPrefix} 02_Player Player.prefab 후보 자동 연결 성공. prefab={candidate.name}");
            break;
        }

        if (playerPrefab == null)
        {
            Debug.LogError($"{LogPrefix} Player.prefab 자동 연결 실패. NetworkProjectConfig PrefabTable에 02_Player/Player.prefab 등록이 필요합니다.");
        }
    }

    private static NetworkObject TryResolveResourcePrefab(NetworkPrefabSourceResource resourceSource)
    {
        try
        {
            resourceSource.Acquire(true);
            return resourceSource.WaitForResult();
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                resourceSource.Release();
            }
            catch
            {
                // ignore
            }
        }
    }

    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        int index = Math.Abs(player.RawEncoded % Math.Max(1, maxPlayers));
        float angle = 360f / Math.Max(1, maxPlayers) * index;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * 2.5f);
        return new Vector3(0f, 1f, 0f) + offset;
    }

    private void ConfigurePlayerObjectIfNeeded(NetworkObject playerObject, string reason)
    {
        if (playerObject == null)
            return;

        PlayerNetworkSetup setup = playerObject.GetComponent<PlayerNetworkSetup>();
        if (setup == null)
            return;

        bool isLocal = playerObject.HasInputAuthority;
        Debug.Log($"{LogPrefix} 로컬/리모트 판별 완료. netId={playerObject.Id}, isLocal={isLocal}, inputAuth={playerObject.InputAuthority}, localPlayer={_runner.LocalPlayer}, reason={reason}");

        if (_configuredLocalStates.TryGetValue(playerObject.Id, out bool configuredState) && configuredState == isLocal)
        {
            Debug.Log($"{LogPrefix} 중복 초기화 방지: 동일 상태 재적용 건너뜀. netId={playerObject.Id}, isLocal={isLocal}, reason={reason}");
            return;
        }

        PlayerAdapter adapter = playerObject.GetComponent<PlayerAdapter>();
        if (adapter == null)
            adapter = playerObject.gameObject.AddComponent<PlayerAdapter>();

        adapter.ApplyNetworkSetup(isLocal, reason);
        _configuredLocalStates[playerObject.Id] = isLocal;
    }

    private IEnumerator ConfigureWhenSpawned(PlayerRef player, string reason)
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (_runner.TryGetPlayerObject(player, out NetworkObject playerObject) && playerObject != null)
            {
                Debug.Log($"{LogPrefix} 플레이어 네트워크 오브젝트 탐색 성공. player={player}, netId={playerObject.Id}, reason={reason}");
                ConfigurePlayerObjectIfNeeded(playerObject, reason);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"{LogPrefix} 플레이어 오브젝트 탐색 타임아웃. player={player}, reason={reason}");
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log($"{LogPrefix} 서버 연결 완료. roomCode={_latestRoomCode}");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"{LogPrefix} 서버 연결 종료. reason={reason}");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"{LogPrefix} 서버 연결 실패. reason={reason}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"{LogPrefix} PlayerJoined. player={player}, isServer={runner.IsServer}, local={runner.LocalPlayer}");

        if (runner.IsServer)
        {
            EnsurePlayerPrefabAssigned();
            if (playerPrefab == null)
                return;

            if (_spawnedPlayers.ContainsKey(player))
            {
                Debug.Log($"{LogPrefix} 중복 스폰 방지: 이미 스폰된 플레이어입니다. player={player}");
                return;
            }

            Vector3 spawnPos = GetSpawnPosition(player);
            Debug.Log($"{LogPrefix} 플레이어 스폰 요청. player={player}, pos={spawnPos}");
            NetworkObject spawned = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            _spawnedPlayers[player] = spawned;
            Debug.Log($"{LogPrefix} 플레이어 네트워크 오브젝트 생성 완료. player={player}, netId={spawned.Id}, prefab={playerPrefab.name}");
            ConfigurePlayerObjectIfNeeded(spawned, "OnPlayerJoined-SpawnImmediate");
        }

        StartCoroutine(ConfigureWhenSpawned(player, "OnPlayerJoined"));
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject spawned) && spawned != null)
        {
            runner.Despawn(spawned);
            _spawnedPlayers.Remove(player);
        }
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        ConfigurePlayerObjectIfNeeded(obj, "OnObjectEnterAOI");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"{LogPrefix} 게임 씬 로드 완료. 플레이어 어댑터 세팅 확인 시작.");

        NetworkObject[] allObjects = FindObjectsOfType<NetworkObject>(true);
        foreach (NetworkObject obj in allObjects)
        {
            ConfigurePlayerObjectIfNeeded(obj, "OnSceneLoadDone");
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        _spawnedPlayers.Clear();
        _configuredLocalStates.Clear();
        Debug.LogWarning($"{LogPrefix} 네트워크 세션 종료. reason={shutdownReason}");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
