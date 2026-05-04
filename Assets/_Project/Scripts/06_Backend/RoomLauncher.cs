using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class RoomLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    private const string LogPrefix = "[06_Backend][RoomLauncher]";
    private const string HostRoundCountPrefKeyPrefix = "06_Backend.HostRoundCount";
    private const string HostSelectedSlotPrefKey = "HostSaveSlot";
    private const string DefaultHostIdentity = "anonymous-host";

    [Header("Scene")]
    [SerializeField] private string startSceneName = "Main_menu";
    [SerializeField] private string gameScenePath = "Assets/_Project/Scenes/06_Backend/TestMain.unity";
    [SerializeField] private string gameSceneNameFallback = "TestMain";

    [Header("Network")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string roomSessionPrefix = string.Empty;
    [SerializeField] private bool forceFixedNetworkTick = true;
    [SerializeField, Range(15, 120)] private int forcedNetworkTickRate = 30;
    [SerializeField] private bool disableVSyncWhenForcingTick = true;
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Vector3 spawnBasePosition = new(1004f, -29f, -993f);
    [SerializeField] private Vector3 spawnPerPlayerOffset = Vector3.zero;

    [Header("Room Code Overlay")]
    [SerializeField] private bool showRoomCodeOverlay = true;
    [SerializeField] private Vector2 roomCodeOverlayOffset = new(16f, -16f);

    private static RoomLauncher _instance;

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _callbacksRegistered;
    private bool _isConnecting;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();
    private readonly Dictionary<NetworkId, bool> _configuredLocalStates = new();
    private readonly HashSet<NetworkId> _pendingAuthorityChecks = new();
    private NetworkObject _cachedLocalPlayerObject;
    private PlayerInputHandler _cachedLocalInputHandler;

    private Button _loginButton;
    private Button _startButton;
    private Button _hostButton;
    private Button _joinButton;
    private Button _enterButton;
    private readonly Dictionary<int, Button> _hostSlotButtons = new();
    private readonly Dictionary<int, Button> _hostConfirmButtons = new();
    private int _pendingHostSlot = -1;
    private bool _hostFlowBound;
    private TMP_InputField _roomCodeInput;
    private InputField _roomCodeInputLegacy;
    private LoadGameMenuController _loadGameMenuController;

    private string _latestRoomCode = string.Empty;
    private Canvas _roomCodeCanvas;
    private TextMeshProUGUI _roomCodeText;

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
        EnsureRoomCodeOverlay(scene);

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
        _joinButton = FindButtonByCandidates("Join Button", "Join", "참가");
        _enterButton = FindButtonByCandidates("Entry Button", "Entry", "Enter", "입장하기", "입장");
        _roomCodeInput = FindRoomCodeInputField();
        _roomCodeInputLegacy = FindRoomCodeInputFieldLegacy();

        bool hasAnyMenuControl = _loginButton != null || _startButton != null || _hostButton != null || _joinButton != null || _enterButton != null || _roomCodeInput != null || _roomCodeInputLegacy != null;
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

        BindHostFlowUi();
        _loadGameMenuController ??= new LoadGameMenuController(this);
        _loadGameMenuController.BindAndRefresh();

        if (_joinButton != null)
        {
            _joinButton.onClick.RemoveListener(OnJoinButtonClicked);
            _joinButton.onClick.AddListener(OnJoinButtonClicked);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} Join Button(참가 버튼)을 찾지 못했습니다.");
        }

        if (_enterButton != null)
        {
            _enterButton.onClick.RemoveListener(OnEnterButtonClicked);
            _enterButton.onClick.AddListener(OnEnterButtonClicked);
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} Entry Button(입장하기 버튼)을 찾지 못했습니다.");
        }

        if (_roomCodeInput == null && _roomCodeInputLegacy == null)
        {
            Debug.LogWarning($"{LogPrefix} 방 코드 입력 InputField(TMP/Legacy)를 찾지 못했습니다. 참가 기능이 동작하지 않을 수 있습니다.");
        }
        else
        {
            string tmpName = _roomCodeInput != null ? _roomCodeInput.name : "null";
            string legacyName = _roomCodeInputLegacy != null ? _roomCodeInputLegacy.name : "null";
            Debug.Log($"{LogPrefix} 방 코드 입력 바인딩 결과. TMP={tmpName}, Legacy={legacyName}");
        }

        return true;
    }


    private void BindHostFlowUi()
    {
        _hostSlotButtons.Clear();
        _hostConfirmButtons.Clear();
        _hostFlowBound = false;

        BindHostSlotButton(1, "Start Game Box1", "Gmae1 Start Box", "Start Box1");
        BindHostSlotButton(2, "Start Game Box2", "Gmae2 Start Box", "Start Box2");
        BindHostSlotButton(3, "Start Game Box3", "Gmae3 Start Box", "Start Box3");

        BindHostConfirmButton(1, "Start Game Confirm PopUp1");
        BindHostConfirmButton(2, "Start Game Confirm PopUp2");
        BindHostConfirmButton(3, "Start Game Confirm PopUp3");

        _hostFlowBound = _hostSlotButtons.Count == 3 && _hostConfirmButtons.Count == 3;
    }

    private void BindHostSlotButton(int slot, params string[] containerCandidates)
    {
        Button button = FindButtonInNamedContainer(containerCandidates);
        if (button == null)
            return;

        button.onClick.AddListener(() => OnHostSlotButtonClicked(slot));
        _hostSlotButtons[slot] = button;
    }

    private void BindHostConfirmButton(int slot, params string[] containerCandidates)
    {
        Button button = FindButtonInNamedContainer(containerCandidates);
        if (button == null)
            return;

        button.onClick.AddListener(() => OnHostConfirmButtonClicked(slot));
        _hostConfirmButtons[slot] = button;
    }

    private static Button FindButtonInNamedContainer(params string[] containerCandidates)
    {
        if (containerCandidates == null || containerCandidates.Length == 0)
            return null;        

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform target in transforms)
        {
            if (!ContainsAny(target.name, containerCandidates))
                continue;

            Button button = target.GetComponentInChildren<Button>(true);
            if (button != null)
                return button;
        }

        return null;
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
        TMP_InputField directMatch = GameObject.Find("Room Number InputField")?.GetComponent<TMP_InputField>();
        if (directMatch != null)
            return directMatch;

        TMP_InputField[] inputFields = FindObjectsOfType<TMP_InputField>(true);
        foreach (TMP_InputField input in inputFields)
        {
            if (ContainsAny(input.name, new[] { "Room Number InputField", "Room Number", "RoomNumberInputField" }))
                return input;

            if (input.placeholder is TMP_Text placeholderText &&
                ContainsAny(placeholderText.text, new[] { "방 번호", "방 코드를", "방 코드" }))
                return input;
        }

        return null;
    }

    private static InputField FindRoomCodeInputFieldLegacy()
    {
        InputField directMatch = GameObject.Find("Room Number InputField")?.GetComponent<InputField>();
        if (directMatch != null)
            return directMatch;

        InputField[] inputFields = FindObjectsOfType<InputField>(true);
        foreach (InputField input in inputFields)
        {
            if (ContainsAny(input.name, new[] { "Room Number InputField", "Room Number", "RoomNumberInputField" }))
                return input;

            if (input.placeholder is Text placeholderText &&
                ContainsAny(placeholderText.text, new[] { "방 번호", "방 코드를", "방 코드" }))
                return input;
        }

        return null;
    }

    private void SetRoomCodeInputText(string roomCode)
    {
        if (_roomCodeInput != null)
            _roomCodeInput.text = roomCode;

        if (_roomCodeInputLegacy != null)
            _roomCodeInputLegacy.text = roomCode;
    }

    private string GetRoomCodeInputText()
    {
        if (_roomCodeInput != null)
            return _roomCodeInput.text;

        if (_roomCodeInputLegacy != null)
            return _roomCodeInputLegacy.text;

        return string.Empty;
    }

    private bool EnsureLoggedInForMenuAction(string actionName)
    {
        if (AuthSession.IsLoggedIn)
            return true;

        Debug.LogWarning($"{LogPrefix} {actionName} 차단: 로그인 성공 전에는 진입할 수 없습니다.");
        return false;
    }

    private void OnLoginButtonClicked()
    {
        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 로그인 버튼 클릭.");
    }

    private void OnStartButtonClicked()
    {
        if (!EnsureLoggedInForMenuAction("시작하기"))
            return;

        _loadGameMenuController?.BindAndRefresh();
        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 시작하기 버튼 클릭. 슬롯 UI 최신화 완료.");
    }

    private void OnHostButtonClicked()
    {
        if (!EnsureLoggedInForMenuAction("호스트"))
            return;

        if (!_hostFlowBound)
        {
            BindHostFlowUi();
            if (!_hostFlowBound)
            {
                Debug.LogWarning($"{LogPrefix} Host 흐름 UI 바인딩 실패. Start Box(1/2/3), Confirm PopUp(1/2/3) 오브젝트 이름을 확인하세요.");
                return;
            }
        }

        _pendingHostSlot = -1;
        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 호스트 버튼 클릭. Start Game Menu에서 저장 슬롯(1/2/3) 선택 대기.");
    }

    private void OnHostSlotButtonClicked(int slot)
    {
        if (!EnsureLoggedInForMenuAction($"호스트 슬롯 {slot}"))
            return;

        _pendingHostSlot = slot;
        Debug.Log($"{LogPrefix} 호스트 슬롯 선택. slot={slot}. Start Game Confirm PopUp{slot} 확인 버튼 대기.");
    }

    private void OnHostConfirmButtonClicked(int slot)
    {
        if (!EnsureLoggedInForMenuAction($"호스트 확인 {slot}"))
            return;

        if (_pendingHostSlot != slot)
        {
            Debug.LogWarning($"{LogPrefix} 확인 팝업 슬롯 불일치. pending={_pendingHostSlot}, clicked={slot}. 슬롯 선택 후 다시 시도하세요.");
            return;
        }

        _pendingHostSlot = -1;
        StartHostGameForSlot(slot);
    }

    private void StartHostGameForSlot(int slot)
    {
        int roomCode = GenerateHostRoomCode();
        string normalizedCode = roomCode.ToString();
        string sessionName = ResolveSessionName(roomCode);

        _latestRoomCode = normalizedCode;
        SetRoomCodeInputText(normalizedCode);
        RefreshRoomCodeOverlay();

        PlayerPrefs.SetInt(HostSelectedSlotPrefKey, slot);
        PlayerPrefs.SetString($"HostSlot{slot}_RoomCode", normalizedCode);
        BackendRoundManager.ResetAllHostSlotRoundsToOne();
        PlayerPrefs.SetString(BuildHostSaveDatePrefKey(slot), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();
        _loadGameMenuController?.RefreshSlotUi();

        Debug.Log($"{LogPrefix} 호스트 방 생성. slot={slot}, 생성된 방 코드(1~1000)={normalizedCode}");
        Debug.Log($"{LogPrefix} 코드 -> 룸 이름 매핑. code={normalizedCode}, session={sessionName}");

        _ = StartGameAsync(normalizedCode, sessionName, GameMode.Host);
    }

    private void OnJoinButtonClicked()
    {
        if (!EnsureLoggedInForMenuAction("참가"))
            return;

        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 참가 버튼 클릭.");
    }

    internal bool CanProceedMenuAction(string actionName)
    {
        return EnsureLoggedInForMenuAction(actionName);
    }

    internal void StartLoadedHostGameForSlot(int slot)
    {
        int roomCode = GenerateHostRoomCode();
        string normalizedCode = roomCode.ToString();
        string sessionName = ResolveSessionName(roomCode);

        _latestRoomCode = normalizedCode;
        SetRoomCodeInputText(normalizedCode);
        RefreshRoomCodeOverlay();

        PlayerPrefs.SetInt(HostSelectedSlotPrefKey, slot);
        PlayerPrefs.SetString($"HostSlot{slot}_RoomCode", normalizedCode);
        PlayerPrefs.Save();

        int round = Mathf.Max(1, PlayerPrefs.GetInt(BuildHostRoundCountPrefKey(slot), 1));
        Debug.Log($"{LogPrefix} 불러오기 시작(Host). slot={slot}, roomCode={normalizedCode}, resumeRound={round}");
        _ = StartGameAsync(normalizedCode, sessionName, GameMode.Host);
    }

    private void OnEnterButtonClicked()
    {
        if (!EnsureLoggedInForMenuAction("입장하기"))
            return;

        Debug.Log($"{LogPrefix} Main_menu 버튼 흐름: 입장하기 버튼 클릭.");
        string rawInputCode = GetRoomCodeInputText().Trim();
        if (!TryParseRoomCode(rawInputCode, out int roomCode))
        {
            Debug.LogWarning($"{LogPrefix} 참가 코드 유효성 실패. 1~1000 범위 숫자만 허용됩니다. raw={rawInputCode}");
            return;
        }

        string normalizedCode = roomCode.ToString();
        Debug.Log($"{LogPrefix} 참가자가 입력한 코드(raw)={rawInputCode}, parsed={roomCode}, normalized={normalizedCode}");

        string sessionName = ResolveSessionName(roomCode);
        Debug.Log($"{LogPrefix} 코드 -> 룸 이름 매핑. code={normalizedCode}, session={sessionName}");
        Debug.Log($"{LogPrefix} 코드 기반 방 접속 시도. code={normalizedCode}, session={sessionName}");

        _ = StartGameAsync(normalizedCode, sessionName, GameMode.Client);
    }

    private static int GenerateHostRoomCode()
    {
        return UnityEngine.Random.Range(1, 1001);
    }

    private static bool TryParseRoomCode(string input, out int roomCode)
    {
        roomCode = 0;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (!int.TryParse(input, out int value))
            return false;

        if (value < 1 || value > 1000)
            return false;

        roomCode = value;
        return true;
    }

    private static bool IsValidRoomCode(string code)
    {
        return TryParseRoomCode(code, out _);
    }

    internal static string BuildHostRoundCountPrefKey(int slot)
    {
        int safeSlot = Mathf.Clamp(slot, 1, 3);
        return $"{HostRoundCountPrefKeyPrefix}.{GetNormalizedHostIdentity()}.slot{safeSlot}";
    }

    internal static string BuildHostSaveDatePrefKey(int slot)
    {
        int safeSlot = Mathf.Clamp(slot, 1, 3);
        return $"HostSlotSavedDate.{GetNormalizedHostIdentity()}.slot{safeSlot}";
    }

    private static string GetNormalizedHostIdentity()
    {
        string rawIdentity = AuthSession.IsLoggedIn ? AuthSession.CurrentUserId : DefaultHostIdentity;
        return string.IsNullOrWhiteSpace(rawIdentity) ? DefaultHostIdentity : rawIdentity.Trim().ToLowerInvariant();
    }

    private string ResolveSessionName(int roomCode)
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
                SceneManager = _sceneManager,
                Config = BuildStartGameConfig()
            });

            if (result.Ok)
            {
                Debug.Log($"{LogPrefix} 방 입장 성공. mode={mode}, code={roomCode}, session={sessionName}");
            }
            else
            {
                Debug.LogError($"{LogPrefix} 방 입장 실패. mode={mode}, code={roomCode}, session={sessionName}, reason={result.ShutdownReason}");

                if (mode == GameMode.Client && result.ShutdownReason == ShutdownReason.GameNotFound)
                {
                    Debug.LogWarning($"{LogPrefix} GameNotFound 진단: {BuildClientJoinDiagnostic(sessionName)}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LogPrefix} 방 입장 중 예외 발생. {ex}");
        }
        finally
        {
            _isConnecting = false;
            RefreshRoomCodeOverlay();
        }
    }

    private bool IsGameScene(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(gameSceneNameFallback) &&
            string.Equals(sceneName, gameSceneNameFallback, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string gameSceneNameFromPath = Path.GetFileNameWithoutExtension(gameScenePath);
        return !string.IsNullOrWhiteSpace(gameSceneNameFromPath) &&
               string.Equals(sceneName, gameSceneNameFromPath, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureRoomCodeOverlay(Scene scene)
    {
        if (!showRoomCodeOverlay || !IsGameScene(scene.name))
            return;

        if (_roomCodeCanvas == null)
        {
            GameObject canvasGo = new("RoomCodeOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);
            DontDestroyOnLoad(canvasGo);
            _roomCodeCanvas = canvasGo.AddComponent<Canvas>();
            _roomCodeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        if (_roomCodeText == null)
        {
            GameObject textGo = new("RoomCodeText");
            textGo.transform.SetParent(_roomCodeCanvas.transform, false);
            _roomCodeText = textGo.AddComponent<TextMeshProUGUI>();
            _roomCodeText.fontSize = 28f;
            _roomCodeText.alignment = TextAlignmentOptions.TopLeft;
            _roomCodeText.color = Color.white;
            _roomCodeText.raycastTarget = false;

            RectTransform rect = _roomCodeText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = roomCodeOverlayOffset;
            rect.sizeDelta = new Vector2(560f, 80f);
        }

        RefreshRoomCodeOverlay();
    }

    private void RefreshRoomCodeOverlay()
    {
        if (_roomCodeText == null)
            return;

        bool isGameScene = IsGameScene(SceneManager.GetActiveScene().name);
        bool isHost = _runner != null && _runner.IsRunning && _runner.IsServer;
        bool canShow = isGameScene && isHost && IsValidRoomCode(_latestRoomCode);
        _roomCodeText.gameObject.SetActive(canShow);
        if (!canShow)
            return;

        _roomCodeText.text = $"ROOM CODE : {_latestRoomCode}";
    }

    private static string BuildClientJoinDiagnostic(string sessionName)
    {
        string appId = "unknown";
        string appVersion = "default";
        string fixedRegion = "best-region(auto)";

        if (PhotonAppSettings.TryGetGlobal(out PhotonAppSettings global) && global != null)
        {
            appId = string.IsNullOrWhiteSpace(global.AppSettings.AppIdFusion) ? "missing" : global.AppSettings.AppIdFusion;
            if (!string.IsNullOrWhiteSpace(global.AppSettings.AppVersion))
                appVersion = global.AppSettings.AppVersion;

            if (!string.IsNullOrWhiteSpace(global.AppSettings.FixedRegion))
                fixedRegion = global.AppSettings.FixedRegion;
        }

        return $"session='{sessionName}', appId='{appId}', appVersion='{appVersion}', fixedRegion='{fixedRegion}'. " +
               "호스트/클라이언트의 AppId, AppVersion, Region(특히 FixedRegion), SessionName이 완전히 동일한지 확인하세요.";
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

        _runner.ProvideInput = true;

        if (!_callbacksRegistered)
        {
            _runner.AddCallbacks(this);
            _callbacksRegistered = true;
        }
    }

    private NetworkProjectConfig BuildStartGameConfig()
    {
        NetworkProjectConfig baseConfig = NetworkProjectConfig.Global;
        if (baseConfig == null)
            return null;

        if (!forceFixedNetworkTick)
            return baseConfig;

        int clampedTickRate = Mathf.Clamp(forcedNetworkTickRate, 15, 120);

        Time.fixedDeltaTime = 1f / clampedTickRate;
        if (disableVSyncWhenForcingTick)
            QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = clampedTickRate;

        Debug.Log($"{LogPrefix} 고정 틱 적용. tickRate={clampedTickRate}, fixedDeltaTime={Time.fixedDeltaTime:0.0000}, vSync={QualitySettings.vSyncCount}, targetFps={Application.targetFrameRate}");
        return baseConfig;
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
        {
            EnsureBackendNetworkSyncOnPrefab(playerPrefab);
            return;
        }

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
            EnsureBackendNetworkSyncOnPrefab(playerPrefab);
            Debug.Log($"{LogPrefix} 02_Player Player.prefab 후보 자동 연결 성공. prefab={candidate.name}");
            break;
        }

        if (playerPrefab == null)
        {
            Debug.LogError($"{LogPrefix} Player.prefab 자동 연결 실패. NetworkProjectConfig PrefabTable에 02_Player/Player.prefab 등록이 필요합니다.");
        }
    }



    private static void EnsureBackendNetworkSyncOnPrefab(NetworkObject prefab)
    {
        if (prefab == null)
            return;

        if (prefab.GetComponent<BackendPlayerNetworkSync>() != null)
            return;

        prefab.gameObject.AddComponent<BackendPlayerNetworkSync>();
        Debug.Log($"{LogPrefix} BackendPlayerNetworkSync를 Player 프리팹에 런타임 추가했습니다.");
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

    private (Vector3 position, Quaternion rotation) GetSpawnPose(PlayerRef player)
    {
        int playerIndex = Mathf.Max(0, player.RawEncoded - 1);
        Vector3 offset = spawnPerPlayerOffset * playerIndex;

        if (Spawner.Instance != null)
        {
            Transform spawnPoint = Spawner.Instance.GetSpawnPoint();
            return (spawnPoint.position + offset, spawnPoint.rotation);
        }

        return (spawnBasePosition + offset, Quaternion.identity);
    }

    private void ConfigurePlayerObjectIfNeeded(NetworkObject playerObject, string reason)
    {
        if (playerObject == null)
            return;

        PlayerNetworkSetup setup = playerObject.GetComponent<PlayerNetworkSetup>();
        if (setup == null)
            return;

        if (playerObject.InputAuthority == PlayerRef.None)
        {
            ScheduleAuthorityReadyReconfigure(playerObject, reason);
            Debug.LogWarning($"{LogPrefix} 로컬/리모트 판별 지연: InputAuthority 미확정. netId={playerObject.Id}, reason={reason}");
            return;
        }

        bool isLocal = _runner != null && playerObject.InputAuthority == _runner.LocalPlayer;
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

        if (isLocal)
        {
            _cachedLocalPlayerObject = playerObject;
            _cachedLocalInputHandler = playerObject.GetComponent<PlayerInputHandler>();
        }
    }

    private void ScheduleAuthorityReadyReconfigure(NetworkObject playerObject, string reason)
    {
        if (playerObject == null)
            return;

        if (_pendingAuthorityChecks.Contains(playerObject.Id))
            return;

        _pendingAuthorityChecks.Add(playerObject.Id);
        StartCoroutine(ConfigureWhenAuthorityReady(playerObject, reason));
    }

    private IEnumerator ConfigureWhenAuthorityReady(NetworkObject playerObject, string reason)
    {
        float timeout = 3f;
        float elapsed = 0f;
        NetworkId targetId = playerObject != null ? playerObject.Id : default;

        while (elapsed < timeout)
        {
            if (playerObject == null)
                break;

            if (playerObject.InputAuthority != PlayerRef.None)
            {
                ConfigurePlayerObjectIfNeeded(playerObject, $"{reason}-AuthorityReady");
                _pendingAuthorityChecks.Remove(targetId);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        _pendingAuthorityChecks.Remove(targetId);
        Debug.LogWarning($"{LogPrefix} InputAuthority 확정 대기 타임아웃. netId={targetId}, reason={reason}");
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

            (Vector3 spawnPos, Quaternion spawnRot) = GetSpawnPose(player);
            Debug.Log($"{LogPrefix} 플레이어 스폰 요청. player={player}, pos={spawnPos}, rot={spawnRot.eulerAngles}");
            NetworkObject spawned = runner.Spawn(
                playerPrefab,
                spawnPos,
                spawnRot,
                player,
                onBeforeSpawned: (_, networkObject) =>
                {
                    if (networkObject != null)
                    {
                        networkObject.transform.SetPositionAndRotation(spawnPos, spawnRot);
                    }
                });

            if (spawned == null)
            {
                Debug.LogError($"{LogPrefix} 플레이어 스폰 실패. player={player}, requestedPos={spawnPos}");
                return;
            }

            spawned.transform.SetPositionAndRotation(spawnPos, spawnRot);
            Debug.Log($"{LogPrefix} 플레이어 스폰 위치 강제 적용 완료. player={player}, actualPos={spawned.transform.position}");
            runner.SetPlayerObject(player, spawned);
            _spawnedPlayers[player] = spawned;
            Debug.Log($"{LogPrefix} 플레이어 네트워크 오브젝트 생성 완료. player={player}, netId={spawned.Id}, prefab={playerPrefab.name}");
            ConfigurePlayerObjectIfNeeded(spawned, "OnPlayerJoined-SpawnImmediate");
        }

        StartCoroutine(ConfigureWhenSpawned(player, "OnPlayerJoined"));
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            runner.SetPlayerObject(player, null);
        }

        if (_spawnedPlayers.TryGetValue(player, out NetworkObject spawned) && spawned != null)
        {
            if (_cachedLocalPlayerObject == spawned)
            {
                _cachedLocalPlayerObject = null;
                _cachedLocalInputHandler = null;
            }

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
        _pendingAuthorityChecks.Clear();
        _cachedLocalPlayerObject = null;
        _cachedLocalInputHandler = null;
        Debug.LogWarning($"{LogPrefix} 네트워크 세션 종료. reason={shutdownReason}");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (runner == null)
            return;

        BackendPlayerNetworkInput payload = default;
        PlayerInputHandler inputHandler = GetOrResolveLocalInputHandler(runner);
        if (inputHandler != null)
        {
            payload.Move = inputHandler.MoveInput;
            payload.Look = inputHandler.LookInput;

            NetworkButtons buttons = default;
            buttons.Set(BackendPlayerNetworkInput.SprintButton, inputHandler.IsSprinting);
            buttons.Set(BackendPlayerNetworkInput.JumpButton, inputHandler.JumpTriggered);
            // Crouch는 trigger가 아닌 hold 상태를 전송해야 패킷 손실 시에도 상태가 꼬이지 않습니다.
            buttons.Set(BackendPlayerNetworkInput.CrouchButton, inputHandler.IsCrouchPressed);
            buttons.Set(BackendPlayerNetworkInput.InteractButton, inputHandler.InteractTriggered);
            buttons.Set(BackendPlayerNetworkInput.ActionButton, inputHandler.ActionTriggered);
            payload.Buttons = buttons;
        }

        input.Set(payload);
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        string tokenInfo = token == null ? "null" : $"{token.Length} bytes";
        Debug.Log($"{LogPrefix} 클라이언트 방 입장 시도 감지. remoteAddress={request.RemoteAddress}, token={tokenInfo}, isServer={runner.IsServer}, mode={runner.Mode}");

        request.Accept();
        Debug.Log($"{LogPrefix} 클라이언트 접속 요청 승인 완료. remoteAddress={request.RemoteAddress}");
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    private PlayerInputHandler GetOrResolveLocalInputHandler(NetworkRunner runner)
    {
        if (_cachedLocalInputHandler != null && _cachedLocalPlayerObject != null)
            return _cachedLocalInputHandler;

        if (!runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject localObject) || localObject == null)
            return null;

        _cachedLocalPlayerObject = localObject;
        _cachedLocalInputHandler = localObject.GetComponent<PlayerInputHandler>();
        return _cachedLocalInputHandler;
    }
}
