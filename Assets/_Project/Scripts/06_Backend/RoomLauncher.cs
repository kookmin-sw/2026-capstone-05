using System;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private int maxPlayers = 8;

    private string _roomCode = string.Empty;
    private string _status = "방 코드를 입력하고 생성 또는 참가를 선택하세요.";

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isConnecting;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnGUI()
    {
        if (SceneManager.GetActiveScene().name != "LauncherScene")
            return;

        const int width = 500;
        const int height = 240;
        Rect area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label("게임 시작");
        GUILayout.Space(12);

        GUILayout.Label("방 코드");
        _roomCode = GUILayout.TextField(_roomCode, 32).Trim();

        GUILayout.Space(12);
        GUI.enabled = !_isConnecting;
        if (GUILayout.Button("방 생성 (Host)", GUILayout.Height(36)))
        {
            StartFromCode(isHost: true);
        }

        if (GUILayout.Button("코드로 참가 (Client)", GUILayout.Height(36)))
        {
            StartFromCode(isHost: false);
        }
        GUI.enabled = true;

        GUILayout.Space(12);
        GUILayout.Label(_status);
        GUILayout.EndArea();
    }

    private void StartFromCode(bool isHost)
    {
        if (_isConnecting)
            return;

        if (string.IsNullOrWhiteSpace(_roomCode))
        {
            _status = "방 코드를 입력해주세요.";
            return;
        }

        _ = StartGame(_roomCode, isHost ? GameMode.Host : GameMode.Client);
    }

    private async Task StartGame(string roomCode, GameMode mode)
    {
        _isConnecting = true;
        _status = $"서버 연결 중... ({roomCode})";

        try
        {
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
                _runner.ProvideInput = true;
                _runner.AddCallbacks(this);
            }

            if (_sceneManager == null)
            {
                _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            int sceneBuildIndex = SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{gameSceneName}.unity");
            if (sceneBuildIndex < 0)
                throw new Exception($"게임 씬({gameSceneName})이 Build Settings에 없습니다.");

            var result = await _runner.StartGame(new StartGameArgs
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateLauncherBootstrap()
    {
        if (FindFirstObjectByType<RoomLauncher>() != null)
            return;

        var launcher = new GameObject(nameof(RoomLauncher));
        launcher.AddComponent<RoomLauncher>();
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        _status = "서버 연결 완료";
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        _status = $"서버 연결 종료: {reason}";
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        _status = $"연결 실패: {reason}";
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
