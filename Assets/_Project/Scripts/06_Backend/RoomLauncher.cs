using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Systems.Inventory;
using FMODUnity;

public class RoomLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    private static RoomLauncher _instance;

    [Header("Launcher")]
    [SerializeField] private string startSceneName = "Start";

    [Header("Network Game")]
    [SerializeField] private string gameScenePath = "Assets/_Project/Scenes/06_Backend/Main1.unity";
    [SerializeField] private string gameSceneNameFallback = "Main1";
    [SerializeField] private int maxPlayers = 8;
    [SerializeField] private string[] playerPrefabNameFallbacks = { "PlayerCharacter", "Player" };
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Input")]
    [SerializeField] private float mouseLookSensitivity = 0.15f;
    [SerializeField] private bool invertMouseY = true;
    [SerializeField] private float gamepadLookSensitivity = 180f;

    private string _roomCode = string.Empty;
    private string _status = "방 코드를 입력하고 생성(Host) 또는 참가(Client) 하세요.";

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isConnecting;
    private bool _callbacksRegistered;
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnGUI()
    {
        if (_runner != null && _runner.IsRunning)
            return;

        if (!IsStartScene(SceneManager.GetActiveScene().name))
            return;

        EnsureMenuCursor();
        DrawRoomMenu();
    }

    private static void EnsureMenuCursor()
    {
        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;

        if (!Cursor.visible)
            Cursor.visible = true;
    }

    private bool IsStartScene(string sceneName)
    {
        return string.Equals(sceneName, startSceneName, StringComparison.Ordinal);
    }

    private void DrawRoomMenu()
    {
        const int width = 560;
        const int height = 320;
        Rect area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label("방 코드 기반 멀티플레이");
        GUILayout.Space(10);

        GUILayout.Label("방 코드");
        _roomCode = GUILayout.TextField(_roomCode, 32).Trim();

        GUILayout.Space(12);
        GUI.enabled = !_isConnecting;

        if (GUILayout.Button("호스트로 시작", GUILayout.Height(36)))
            StartFromCode(GameMode.Host);

        if (GUILayout.Button("클라이언트로 참가", GUILayout.Height(36)))
            StartFromCode(GameMode.Client);


        GUI.enabled = true;
        GUILayout.Space(12);
        GUILayout.Label(_status);
        GUILayout.EndArea();
    }

    private void StartFromCode(GameMode mode)
    {
        if (_isConnecting)
            return;

        if (string.IsNullOrWhiteSpace(_roomCode))
        {
            _status = mode == GameMode.Host
                ? "호스트 방 코드를 입력해주세요."
                : "참가할 방 코드를 입력해주세요.";
            return;
        }

        _ = StartGameAsync(_roomCode, mode);
    }

    private async Task StartGameAsync(string roomCode, GameMode mode)
    {
        _isConnecting = true;
        _status = $"연결 중... ({roomCode})";

        try
        {
            EnsureRunnerReady();

            if (_runner.IsRunning)
                await _runner.Shutdown();

            _spawnedPlayers.Clear();

            int sceneBuildIndex = ResolveGameSceneBuildIndex();
            if (sceneBuildIndex < 0)
            {
                throw new Exception($"게임 씬을 찾지 못했습니다. Build Settings에 '{gameScenePath}' 또는 '{gameSceneNameFallback}'를 추가하세요.");
            }

            StartGameResult result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = roomCode,
                PlayerCount = maxPlayers,
                Scene = SceneRef.FromIndex(sceneBuildIndex),
                SceneManager = _sceneManager
            });

            _status = result.Ok ? "게임 시작 성공" : $"시작 실패: {result.ShutdownReason}";
        }
        catch (Exception ex)
        {
            _status = $"오류: {ex.Message}";
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
                _runner = gameObject.AddComponent<NetworkRunner>();
        }

        _runner.ProvideInput = true;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<RoomLauncher>() != null)
            return;

        GameObject go = new GameObject(nameof(RoomLauncher));
        go.AddComponent<RoomLauncher>();
    }

    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        int index = Math.Abs(player.RawEncoded % maxPlayers);
        float angle = (360f / Mathf.Max(1, maxPlayers)) * index;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * 2.5f);
        return new Vector3(0f, 1f, 0f) + offset;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        EnsureFmodListenerOnMainCamera();
        _status = "서버 연결 완료";
    }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) => _status = $"연결 종료: {reason}";
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) => _status = $"연결 실패: {reason}";

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        EnsurePlayerPrefabAssigned();

        if (playerPrefab == null)
        {
            _status = "Player Prefab을 찾지 못했습니다. NetworkProjectConfig의 PrefabTable에 플레이어 프리팹을 등록하세요.";
            return;
        }

        if (_spawnedPlayers.ContainsKey(player))
            return;

        NetworkObject playerObject = runner.Spawn(playerPrefab, GetSpawnPosition(player), Quaternion.identity, player);
        _spawnedPlayers[player] = playerObject;
    }

    private void EnsurePlayerPrefabAssigned()
    {
        if (playerPrefab != null)
            return;

        if (NetworkProjectConfig.Global == null || NetworkProjectConfig.Global.PrefabTable == null)
            return;

        foreach ((NetworkPrefabId _, INetworkPrefabSource source) in NetworkProjectConfig.Global.PrefabTable.GetEntries())
        {
            NetworkObject candidate = TryResolvePrefab(source);
            if (candidate == null)
                continue;

            if (!IsPlayerPrefabName(candidate.name))
                continue;

            playerPrefab = candidate;
            _status = $"Player Prefab 자동 할당: {candidate.name}";
            return;
        }
    }

    private NetworkObject TryResolvePrefab(INetworkPrefabSource source)
    {
        switch (source)
        {
            case NetworkPrefabSourceStatic staticSource:
                return staticSource.Object;
            case NetworkPrefabSourceStaticLazy staticLazySource:
                return staticLazySource.Object.asset;
            case NetworkPrefabSourceResource resourceSource:
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
                        // ignore release failures for unresolved resources
                    }
                }
            default:
                return null;
        }
    }

    private bool IsPlayerPrefabName(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
            return false;

        if (playerPrefabNameFallbacks == null || playerPrefabNameFallbacks.Length == 0)
            return false;

        for (int i = 0; i < playerPrefabNameFallbacks.Length; i++)
        {
            string expectedName = playerPrefabNameFallbacks[i];
            if (string.IsNullOrWhiteSpace(expectedName))
                continue;

            if (string.Equals(prefabName, expectedName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObject) && playerObject != null)
        {
            runner.Despawn(playerObject);
            _spawnedPlayers.Remove(player);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        Vector2 move = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.01f)
                move += stick;
        }

        data.Move = Vector2.ClampMagnitude(move, 1f);

        Vector2 look = Vector2.zero;
        bool inventoryOpen = InventoryView.IsAnyInventoryOpen;

        if (!inventoryOpen)
        {
            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseLookSensitivity;
                if (invertMouseY)
                    mouseDelta.y *= -1f;

                look += mouseDelta;
            }

            if (Gamepad.current != null)
                look += Gamepad.current.rightStick.ReadValue() * gamepadLookSensitivity * runner.DeltaTime;
        }

        data.Look = look;

        if (IsJumpPressed())
        {
            data.Buttons.Set(NetworkInputData.Jump, true);
        }

        if (IsSprintPressed())
        {
            data.Buttons.Set(NetworkInputData.Sprint, true);
        }

        if (IsCrouchPressed())
        {
            data.Buttons.Set(NetworkInputData.Crouch, true);
        }

        input.Set(data);
    }

    private static bool IsJumpPressed()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
    }

    private static bool IsSprintPressed()
    {
        return (Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)) ||
               (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
    }

    private static bool IsCrouchPressed()
    {
        return (Keyboard.current != null &&
                (Keyboard.current.cKey.isPressed ||
                 Keyboard.current.leftCtrlKey.isPressed ||
                 Keyboard.current.rightCtrlKey.isPressed)) ||
               (Gamepad.current != null && Gamepad.current.buttonEast.isPressed);
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        _spawnedPlayers.Clear();
        _status = $"세션 종료: {shutdownReason}";
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        EnsureFmodListenerOnMainCamera();
    }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    private static void EnsureFmodListenerOnMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        if (mainCamera.GetComponent<AudioListener>() == null)
            mainCamera.gameObject.AddComponent<AudioListener>();

        if (mainCamera.GetComponent<StudioListener>() == null)
            mainCamera.gameObject.AddComponent<StudioListener>();
    }

}
