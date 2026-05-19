using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BackendRoundManager : NetworkBehaviour
{
    private const string HostSelectedSlotPrefKey = "HostSaveSlot";
    public static BackendRoundManager Instance { get; private set; }
    public static event System.Action<NetworkWeatherState> WeatherStateChanged;

    [Header("Round")]
    [SerializeField] private float roundDurationSeconds = 300f;
    public float RoundDurationSeconds => roundDurationSeconds;

    [Header("Weather")]
    [SerializeField] private NetworkWeatherState initialWeatherState = NetworkWeatherState.Snow;

    [Networked] private NetworkBool NetworkIsRoundRunning { get; set; }
    private bool _offlineIsRoundRunning;
    public bool IsRoundRunning 
    {
        get 
        {
            if (PlayerNetworkSetup.IsOfflineTestMode) return _offlineIsRoundRunning;
            return Object != null && Object.IsValid && NetworkIsRoundRunning;
        }
        private set
        {
            if (PlayerNetworkSetup.IsOfflineTestMode) _offlineIsRoundRunning = value;
            else NetworkIsRoundRunning = value;
        }
    }
    
    [Networked] public TickTimer RoundTimer { get; private set; }
    
    [Networked] private int NetworkCurrentRoundNumber { get; set; }
    private int _offlineCurrentRoundNumber;
    public int CurrentRoundNumber 
    {
        get => PlayerNetworkSetup.IsOfflineTestMode ? _offlineCurrentRoundNumber : NetworkCurrentRoundNumber;
        private set
        {
            if (PlayerNetworkSetup.IsOfflineTestMode) _offlineCurrentRoundNumber = value;
            else NetworkCurrentRoundNumber = value;
        }
    }
    
    [Networked] private int NetworkWeatherStateRaw { get; set; }
    private int _offlineWeatherStateRaw;
    private int WeatherStateRaw
    {
        get => PlayerNetworkSetup.IsOfflineTestMode ? _offlineWeatherStateRaw : NetworkWeatherStateRaw;
        set
        {
            if (PlayerNetworkSetup.IsOfflineTestMode) _offlineWeatherStateRaw = value;
            else NetworkWeatherStateRaw = value;
        }
    }
    
    [Networked, OnChangedRender(nameof(OnSharedGoldChanged))] private int NetworkSharedGold { get; set; }
    private int _offlineSharedGold;
    public int SharedGold 
    {
        get => PlayerNetworkSetup.IsOfflineTestMode ? _offlineSharedGold : NetworkSharedGold;
        set
        {
            if (PlayerNetworkSetup.IsOfflineTestMode) 
            {
                _offlineSharedGold = value;
                OnSharedGoldChanged();
            }
            else 
            {
                NetworkSharedGold = value;
            }
        }
    }

    public static event System.Action<int> SharedGoldUpdated;

    private void OnSharedGoldChanged()
    {
        SharedGoldUpdated?.Invoke(SharedGold);
        
        if (HasStateAuthority && global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
        {
            var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
            if (invModel != null && invModel.Gold != SharedGold)
            {
                invModel.SetGold(SharedGold);
            }
        }
    }

    private string _hostRoundCountPrefKey = RoomLauncher.BuildHostRoundCountPrefKey(1);
    private int _activeHostSlot = 1;
    private NetworkWeatherState _lastBroadcastWeatherState;
    
    private Coroutine _offlineTimerCoroutine;
    private float _offlineTimeRemaining;

    public NetworkWeatherState CurrentWeatherState 
    {
        get
        {
            if (PlayerNetworkSetup.IsOfflineTestMode) return (NetworkWeatherState)WeatherStateRaw;
            if (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid)
            {
                return initialWeatherState;
            }
            return (NetworkWeatherState)WeatherStateRaw;
        }
    }

    public float RoundTimeRemainingSeconds
    {
        get
        {
            if (PlayerNetworkSetup.IsOfflineTestMode) return _offlineTimeRemaining;
            if (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid)
            {
                return 0f;
            }

            if (!IsRoundRunning)
            {
                return 0f;
            }

            return RoundTimer.RemainingTime(Runner) ?? 0f;
        }
    }

    private void Start()
    {
        if (PlayerNetworkSetup.IsOfflineTestMode)
        {
            if (Instance == null)
            {
                Instance = this;
            }

            _activeHostSlot = Mathf.Clamp(PlayerPrefs.GetInt(HostSelectedSlotPrefKey, 1), 1, 3);
            _hostRoundCountPrefKey = RoomLauncher.BuildHostRoundCountPrefKey(_activeHostSlot);
            CurrentRoundNumber = Mathf.Max(1, PlayerPrefs.GetInt(_hostRoundCountPrefKey, 1));
            IsRoundRunning = false;
            WeatherStateRaw = (int)initialWeatherState;
            BroadcastWeatherState(force: true);
        }
    }

    public override void Spawned()
    {
        if (!RegisterInstance())
        {
            return;
        }

        if (!HasStateAuthority)
        {
            BroadcastWeatherState(force: true);
            return;
        }

        _activeHostSlot = Mathf.Clamp(PlayerPrefs.GetInt(HostSelectedSlotPrefKey, 1), 1, 3);
        _hostRoundCountPrefKey = RoomLauncher.BuildHostRoundCountPrefKey(_activeHostSlot);
        CurrentRoundNumber = Mathf.Max(1, PlayerPrefs.GetInt(_hostRoundCountPrefKey, 1));
        IsRoundRunning = false;
        RoundTimer = TickTimer.None;
        WeatherStateRaw = (int)initialWeatherState;
        
        if (global::Systems.GridInventory.GridInventory.Instance != null && global::Systems.GridInventory.GridInventory.Instance.Controller != null)
        {
            var invModel = global::Systems.GridInventory.GridInventory.Instance.Controller.Model;
            if (invModel != null)
            {
                SharedGold = invModel.Gold;
            }
        }
        
        BroadcastWeatherState(force: true);

        Debug.Log($"[BackendRoundManager] 호스트 라운드 저장소 로드. key={_hostRoundCountPrefKey}, userId={AuthSession.CurrentUserId}, round={CurrentRoundNumber}");
    }

    private bool RegisterInstance()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
            return true;
        }

        if (IsPreferredInstanceOver(Instance))
        {
            Debug.LogWarning($"[BackendRoundManager] Replacing duplicate instance {Instance.name} with preferred instance {name}.");
            Instance = this;
            return true;
        }

        Debug.LogWarning($"[BackendRoundManager] Duplicate ignored on {name}. Active instance is {Instance.name}. Remove extra BackendRoundManager components from the scene/prefab.");
        return false;
    }

    private bool IsPreferredInstanceOver(BackendRoundManager other)
    {
        if (other == null)
        {
            return true;
        }

        bool thisIsDedicatedManager = gameObject.name == nameof(BackendRoundManager);
        bool otherIsDedicatedManager = other.gameObject.name == nameof(BackendRoundManager);
        return thisIsDedicatedManager && !otherIsDedicatedManager;
    }

    public override void FixedUpdateNetwork()
    {
        if (PlayerNetworkSetup.IsOfflineTestMode) return; // 싱글 모드에서는 무시
        
        if (Instance != this || Object == null || !Object.IsValid)
        {
            return;
        }

        BroadcastWeatherState();

        if (!HasStateAuthority)
        {
            return;
        }

        if (IsRoundRunning && RoundTimer.Expired(Runner))
        {
            EndRound("Timeout");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestSetRoundState(PlayerRef requestedBy, NetworkBool shouldRun)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (shouldRun)
        {
            if (!IsRoundRunning)
            {
                StartRound($"RequestedBy:{requestedBy}");
            }

            return;
        }

        // 라운드는 시간이 만료될 때만 종료된다. 시작 트리거와 다시 상호작용해도 종료 요청은 무시한다.
        Debug.Log($"[BackendRoundManager] 라운드 종료 요청 무시. requestedBy={requestedBy}, shouldRun={shouldRun}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestMovePlayerToSpawner(PlayerRef requestedBy)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        MovePlayerToSpawner(requestedBy);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestSetWeatherState(PlayerRef requestedBy, int weatherState)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        SetWeatherStateInternal((NetworkWeatherState)weatherState);
        Debug.Log($"[BackendRoundManager] Weather state changed. requestedBy={requestedBy}, state={(NetworkWeatherState)weatherState}");
    }

    public void StartRoundOfflineFallback()
    {
        if (!PlayerNetworkSetup.IsOfflineTestMode) return;
        
        // 싱글 모드에서는 IsRoundRunning 프로퍼티를 통해 로컬 변수에 할당됨
        IsRoundRunning = true;
        
        if (_offlineTimerCoroutine != null)
        {
            StopCoroutine(_offlineTimerCoroutine);
        }
        _offlineTimeRemaining = roundDurationSeconds;
        _offlineTimerCoroutine = StartCoroutine(OfflineTimerRoutine());
        
        PlayerPrefs.SetInt(_hostRoundCountPrefKey, CurrentRoundNumber);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(_activeHostSlot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();

        RoundDayOverlay.Show(CurrentRoundNumber);

        Debug.Log($"[BackendRoundManager] 싱글 오프라인 라운드 시작. round={CurrentRoundNumber}");
    }

    private System.Collections.IEnumerator OfflineTimerRoutine()
    {
        while (_offlineTimeRemaining > 0f)
        {
            _offlineTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        _offlineTimeRemaining = 0f;
        EndRound("TimeExpired_Offline");
    }

    public void ForceEndRoundSoon()
    {
        if (PlayerNetworkSetup.IsOfflineTestMode)
        {
            if (IsRoundRunning)
            {
                _offlineTimeRemaining = 1f;
                Debug.Log("[BackendRoundManager] 오프라인 라운드 남은 시간을 1초로 단축했습니다. (테스트)");
            }
            return;
        }

        if (Runner != null && IsRoundRunning)
        {
            RpcRequestForceEndRoundSoon();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestForceEndRoundSoon()
    {
        if (HasStateAuthority && IsRoundRunning)
        {
            RoundTimer = TickTimer.CreateFromSeconds(Runner, 1f);
            Debug.Log("[BackendRoundManager] 클라이언트 요청으로 라운드 남은 시간을 1초로 단축했습니다. (테스트)");
        }
    }

    private void StartRound(string reason)
    {
        IsRoundRunning = true;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDurationSeconds);

        PlayerPrefs.SetInt(_hostRoundCountPrefKey, CurrentRoundNumber);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(_activeHostSlot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();

        if (Runner != null && Runner.IsRunning)
        {
            RpcShowRoundDay(CurrentRoundNumber);
        }
        else
        {
            RoundDayOverlay.Show(CurrentRoundNumber);
        }

        Debug.Log($"[BackendRoundManager] 라운드 시작. round={CurrentRoundNumber}, reason={reason}, duration={roundDurationSeconds}s");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcShowRoundDay(int roundNumber)
    {
        RoundDayOverlay.Show(roundNumber);
    }

    private void EndRound(string reason)
    {
        IsRoundRunning = false;
        if (!PlayerNetworkSetup.IsOfflineTestMode)
        {
            RoundTimer = TickTimer.None;
        }
        CurrentRoundNumber += 1;

        if (_offlineTimerCoroutine != null)
        {
            StopCoroutine(_offlineTimerCoroutine);
            _offlineTimerCoroutine = null;
        }

        PlayerPrefs.SetInt(_hostRoundCountPrefKey, CurrentRoundNumber);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(_activeHostSlot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();

        RespawnAllPlayersAtSpawner();
        
        if (PlayerNetworkSetup.IsOfflineTestMode)
        {
            if (Systems.GridInventory.GridInventory.Instance != null && Systems.GridInventory.GridInventory.Instance.Controller != null)
            {
                Systems.GridInventory.GridInventorySaveSystem.SaveInventory(Systems.GridInventory.GridInventory.Instance.Controller.Model);
            }
            if (Systems.Loot.LootNetworkSync.Instance != null)
            {
                Systems.Loot.LootNetworkSync.Instance.SaveAllLoots();
            }
        }
        else if (BackendPlayerNetworkSync.LocalInstance != null)
        {
            BackendPlayerNetworkSync.LocalInstance.HostInitiateSaveAll();
        }

        Debug.Log($"[BackendRoundManager] 라운드 종료. slot={_activeHostSlot}, round={CurrentRoundNumber}, reason={reason}");
    }

    public void RequestSetWeatherState(NetworkWeatherState weatherState)
    {
        if (PlayerNetworkSetup.IsOfflineTestMode)
        {
            SetWeatherStateInternal(weatherState);
            return;
        }

        PlayerRef requester = Runner != null ? Runner.LocalPlayer : PlayerRef.None;

        if (HasStateAuthority)
        {
            SetWeatherStateInternal(weatherState);
            return;
        }

        if (Runner != null)
        {
            RpcRequestSetWeatherState(requester, (int)weatherState);
        }
    }

    private void RespawnAllPlayersAtSpawner()
    {
        if (Runner == null)
        {
            return;
        }

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) || playerObject == null)
            {
                continue;
            }

            MovePlayerObjectToSpawner(playerObject);
        }
    }

    private void MovePlayerToSpawner(PlayerRef playerRef)
    {
        if (Runner == null || playerRef == PlayerRef.None)
        {
            return;
        }

        if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) || playerObject == null)
        {
            return;
        }

        MovePlayerObjectToSpawner(playerObject);
    }

    private void MovePlayerObjectToSpawner(NetworkObject playerObject)
    {
        PlayerRespawn playerRespawn = playerObject.GetComponent<PlayerRespawn>();
        if (playerRespawn != null)
        {
            playerRespawn.SpawnAtSpawner();
            return;
        }

        if (Spawner.Instance == null)
        {
            return;
        }

        Transform spawnPoint = Spawner.Instance.GetSpawnPoint();
        CharacterController characterController = playerObject.GetComponent<CharacterController>();

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        playerObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.currentVelocity = Vector3.zero;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    public static void ResetAllHostSlotRoundsToOne()
    {
        for (int slot = 1; slot <= 3; slot++)
        {
            ResetHostSlotRoundToOne(slot, false);
        }

        PlayerPrefs.Save();
    }

    public static void ResetHostSlotRoundToOne(int slot)
    {
        ResetHostSlotRoundToOne(slot, true);
    }

    private static void ResetHostSlotRoundToOne(int slot, bool save)
    {
        string key = RoomLauncher.BuildHostRoundCountPrefKey(slot);
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(slot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        if (save)
        {
            PlayerPrefs.Save();
        }
    }

    private void SetWeatherStateInternal(NetworkWeatherState weatherState)
    {
        WeatherStateRaw = (int)weatherState;
        BroadcastWeatherState(force: true);
    }

    private void BroadcastWeatherState(bool force = false)
    {
        if (!PlayerNetworkSetup.IsOfflineTestMode && (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid)) return;

        NetworkWeatherState weatherState = CurrentWeatherState;
        if (!force && _lastBroadcastWeatherState == weatherState)
        {
            return;
        }

        _lastBroadcastWeatherState = weatherState;
        WeatherStateChanged?.Invoke(weatherState);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

}
