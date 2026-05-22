using FMODUnity;
using Fusion;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BackendRoundManager : NetworkBehaviour
{
    private const string HostSelectedSlotPrefKey = "HostSaveSlot";
    private const string DemoThankYouMessage = "데모버전을 플레이해주셔서 감사합니다!";
    public static BackendRoundManager Instance { get; private set; }
    public static event System.Action<NetworkWeatherState> WeatherStateChanged;

    [Header("Round")]
    [SerializeField] private float roundDurationSeconds = 300f;
    [SerializeField] private float roundEndFadeInSeconds = 0.9f;
    [SerializeField] private float roundEndBlackHoldSeconds = 1.8f;
    [SerializeField] private float roundEndFadeOutSeconds = 0.9f;
    public float RoundDurationSeconds => roundDurationSeconds;

    [Header("Weather")]
    [SerializeField] private NetworkWeatherState initialWeatherState = NetworkWeatherState.Snow;

    [Header("Sound Settings")]
    [SerializeField] private EventReference roundStartSound;

    [Networked] private NetworkBool NetworkIsRoundRunning { get; set; }
    private bool _offlineIsRoundRunning;
    public bool IsRoundRunning 
    {
        get 
        {
            if (AuthSession.IsOffline) return _offlineIsRoundRunning;
            return Object != null && Object.IsValid && NetworkIsRoundRunning;
        }
        private set
        {
            if (AuthSession.IsOffline) _offlineIsRoundRunning = value;
            else NetworkIsRoundRunning = value;
        }
    }
    
    [Networked] public TickTimer RoundTimer { get; private set; }
    [Networked] private NetworkBool NetworkHasRoundTimerStarted { get; set; }
    private bool _offlineHasRoundTimerStarted;
    private bool HasRoundTimerStarted
    {
        get => AuthSession.IsOffline ? _offlineHasRoundTimerStarted : NetworkHasRoundTimerStarted;
        set
        {
            if (AuthSession.IsOffline) _offlineHasRoundTimerStarted = value;
            else NetworkHasRoundTimerStarted = value;
        }
    }
    
    [Networked] private int NetworkCurrentRoundNumber { get; set; }
    private int _offlineCurrentRoundNumber;
    public int CurrentRoundNumber 
    {
        get => AuthSession.IsOffline ? _offlineCurrentRoundNumber : NetworkCurrentRoundNumber;
        private set
        {
            if (AuthSession.IsOffline) _offlineCurrentRoundNumber = value;
            else NetworkCurrentRoundNumber = value;
        }
    }
    
    [Networked] private int NetworkWeatherStateRaw { get; set; }
    private int _offlineWeatherStateRaw;
    private int WeatherStateRaw
    {
        get => AuthSession.IsOffline ? _offlineWeatherStateRaw : NetworkWeatherStateRaw;
        set
        {
            if (AuthSession.IsOffline) _offlineWeatherStateRaw = value;
            else NetworkWeatherStateRaw = value;
        }
    }
    
    [Networked, OnChangedRender(nameof(OnSharedGoldChanged))] private int NetworkSharedGold { get; set; }
    private int _offlineSharedGold;
    public int SharedGold 
    {
        get => AuthSession.IsOffline ? _offlineSharedGold : NetworkSharedGold;
        set
        {
            if (AuthSession.IsOffline) 
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
    private bool isEndingRound;
    private bool offlinePlayerExitedBunker;
    private bool offlinePlayerCompletedQuest3;
    private readonly HashSet<PlayerRef> playersWhoExitedBunker = new();
    private readonly HashSet<PlayerRef> playersWhoCompletedQuest3 = new();

    public NetworkWeatherState CurrentWeatherState 
    {
        get
        {
            if (AuthSession.IsOffline) return (NetworkWeatherState)WeatherStateRaw;
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
            if (AuthSession.IsOffline)
            {
                if (!IsRoundRunning)
                {
                    return 0f;
                }

                return HasRoundTimerStarted ? _offlineTimeRemaining : roundDurationSeconds;
            }

            if (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid)
            {
                return 0f;
            }

            if (!IsRoundRunning)
            {
                return 0f;
            }

            if (!HasRoundTimerStarted)
            {
                return roundDurationSeconds;
            }

            return RoundTimer.RemainingTime(Runner) ?? 0f;
        }
    }

    private void Start()
    {
        if (AuthSession.IsOffline)
        {
            if (Instance == null)
            {
                Instance = this;
            }

            _activeHostSlot = Mathf.Clamp(PlayerPrefs.GetInt(HostSelectedSlotPrefKey, 1), 1, 3);
            _hostRoundCountPrefKey = RoomLauncher.BuildHostRoundCountPrefKey(_activeHostSlot);
            CurrentRoundNumber = Mathf.Max(1, PlayerPrefs.GetInt(_hostRoundCountPrefKey, 1));
            IsRoundRunning = false;
            HasRoundTimerStarted = false;
            _offlineTimeRemaining = roundDurationSeconds;
            ResetRoundGateStateLocal();
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
        HasRoundTimerStarted = false;
        ResetRoundGateStateLocal();
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
        if (AuthSession.IsOffline) return; // 싱글 모드에서는 무시
        
        if (Instance != this || Object == null || !Object.IsValid)
        {
            return;
        }

        BroadcastWeatherState();

        if (!HasStateAuthority)
        {
            return;
        }

        if (IsRoundRunning && HasRoundTimerStarted && RoundTimer.Expired(Runner))
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
        if (!AuthSession.IsOffline) return;
        
        // 싱글 모드에서는 IsRoundRunning 프로퍼티를 통해 로컬 변수에 할당됨
        IsRoundRunning = true;
        HasRoundTimerStarted = false;
        
        if (_offlineTimerCoroutine != null)
        {
            StopCoroutine(_offlineTimerCoroutine);
            _offlineTimerCoroutine = null;
        }
        _offlineTimeRemaining = roundDurationSeconds;
        ResetRoundGateStateLocal();
        
        PlayerPrefs.SetInt(_hostRoundCountPrefKey, CurrentRoundNumber);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(_activeHostSlot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();

        RoundDayOverlay.Show(CurrentRoundNumber);

        Debug.Log($"[BackendRoundManager] 싱글 오프라인 라운드 시작. round={CurrentRoundNumber}");
        RuntimeManager.PlayOneShot(roundStartSound);
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
        if (AuthSession.IsOffline)
        {
            if (IsRoundRunning)
            {
                if (!HasRoundTimerStarted)
                {
                    StartRoundTimer("ForceEndSoon_Offline");
                }

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
            HasRoundTimerStarted = true;
            RoundTimer = TickTimer.CreateFromSeconds(Runner, 1f);
            Debug.Log("[BackendRoundManager] 클라이언트 요청으로 라운드 남은 시간을 1초로 단축했습니다. (테스트)");
        }
    }

    private void StartRound(string reason)
    {
        if (isEndingRound)
        {
            return;
        }

        IsRoundRunning = true;
        HasRoundTimerStarted = false;
        RoundTimer = TickTimer.None;
        ResetRoundGateStateLocal();
        if (Runner != null && Runner.IsRunning)
        {
            RpcResetRoundGateState();
        }

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
        RuntimeManager.PlayOneShot(roundStartSound);
    }

    public void RegisterPlayerBunkerExit(PlayerRef playerRef)
    {
        if (AuthSession.IsOffline)
        {
            offlinePlayerExitedBunker = true;
            StartRoundTimer("OfflinePlayerExitedBunker");
            return;
        }

        if (playerRef == PlayerRef.None)
        {
            return;
        }

        if (HasStateAuthority)
        {
            RegisterPlayerBunkerExitInternal(playerRef);
            return;
        }

        if (Runner != null && Object != null && Object.IsValid)
        {
            RpcRequestRegisterPlayerBunkerExit(playerRef);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestRegisterPlayerBunkerExit(PlayerRef playerRef)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        RegisterPlayerBunkerExitInternal(playerRef);
    }

    private void RegisterPlayerBunkerExitInternal(PlayerRef playerRef)
    {
        if (playerRef == PlayerRef.None)
        {
            return;
        }

        if (playersWhoExitedBunker.Add(playerRef))
        {
            RpcSyncPlayerBunkerGateState(playerRef, true, playersWhoCompletedQuest3.Contains(playerRef));
        }

        StartRoundTimer($"PlayerExitedBunker:{playerRef}");
    }

    public void MarkLocalPlayerQuest3Completed()
    {
        if (AuthSession.IsOffline)
        {
            MarkPlayerQuest3Completed(PlayerRef.None);
            return;
        }

        PlayerRef playerRef = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
        if (playerRef == PlayerRef.None &&
            LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController player) &&
            player != null)
        {
            NetworkObject playerObject = player.GetComponent<NetworkObject>();
            playerRef = playerObject != null ? playerObject.InputAuthority : PlayerRef.None;
        }

        Debug.Log($"[BackendRoundManager] Quest 3 complete report. localPlayer={(Runner != null ? Runner.LocalPlayer : PlayerRef.None)}, resolved={playerRef}, hasStateAuthority={HasStateAuthority}");

        if (!HasStateAuthority && playerRef != PlayerRef.None)
        {
            playersWhoCompletedQuest3.Add(playerRef);
        }

        MarkPlayerQuest3Completed(playerRef);
    }

    public void MarkPlayerQuest3Completed(PlayerRef playerRef)
    {
        if (AuthSession.IsOffline)
        {
            offlinePlayerCompletedQuest3 = true;
            return;
        }

        if (playerRef == PlayerRef.None)
        {
            return;
        }

        if (HasStateAuthority)
        {
            MarkPlayerQuest3CompletedInternal(playerRef);
            return;
        }

        if (Runner != null && Object != null && Object.IsValid)
        {
            RpcRequestMarkPlayerQuest3Completed(playerRef);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestMarkPlayerQuest3Completed(PlayerRef playerRef, RpcInfo rpcInfo = default)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        PlayerRef resolvedPlayerRef = rpcInfo.Source != PlayerRef.None ? rpcInfo.Source : playerRef;
        Debug.Log($"[BackendRoundManager] Quest 3 complete RPC. source={rpcInfo.Source}, requested={playerRef}, resolved={resolvedPlayerRef}");
        MarkPlayerQuest3CompletedInternal(resolvedPlayerRef);
    }

    private void MarkPlayerQuest3CompletedInternal(PlayerRef playerRef)
    {
        if (playerRef == PlayerRef.None)
        {
            return;
        }

        if (playersWhoCompletedQuest3.Add(playerRef))
        {
            RpcSyncPlayerBunkerGateState(playerRef, playersWhoExitedBunker.Contains(playerRef), true);
        }
    }

    public bool CanPlayerEnterBunker(PlayerRef playerRef)
    {
        if (AuthSession.IsOffline)
        {
            return !offlinePlayerExitedBunker || offlinePlayerCompletedQuest3;
        }

        if (playerRef == PlayerRef.None)
        {
            return false;
        }

        return !playersWhoExitedBunker.Contains(playerRef) || playersWhoCompletedQuest3.Contains(playerRef);
    }

    public string GetPlayerBunkerGateStateDebug(PlayerRef playerRef)
    {
        if (AuthSession.IsOffline)
        {
            return $"offlineExited={offlinePlayerExitedBunker}, offlineCompletedQuest3={offlinePlayerCompletedQuest3}";
        }

        return $"playerRef={playerRef}, exited={playersWhoExitedBunker.Contains(playerRef)}, completedQuest3={playersWhoCompletedQuest3.Contains(playerRef)}";
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcSyncPlayerBunkerGateState(PlayerRef playerRef, NetworkBool hasExitedBunker, NetworkBool hasCompletedQuest3)
    {
        if (playerRef == PlayerRef.None)
        {
            return;
        }

        if (hasExitedBunker)
        {
            playersWhoExitedBunker.Add(playerRef);
        }
        else
        {
            playersWhoExitedBunker.Remove(playerRef);
        }

        if (hasCompletedQuest3)
        {
            playersWhoCompletedQuest3.Add(playerRef);
        }
        else
        {
            playersWhoCompletedQuest3.Remove(playerRef);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcResetRoundGateState()
    {
        ResetRoundGateStateLocal();
    }

    private void ResetRoundGateStateLocal()
    {
        offlinePlayerExitedBunker = false;
        offlinePlayerCompletedQuest3 = false;
        playersWhoExitedBunker.Clear();
        playersWhoCompletedQuest3.Clear();
    }

    private void StartRoundTimer(string reason)
    {
        if (!IsRoundRunning || HasRoundTimerStarted)
        {
            return;
        }

        HasRoundTimerStarted = true;

        if (AuthSession.IsOffline)
        {
            if (_offlineTimerCoroutine != null)
            {
                StopCoroutine(_offlineTimerCoroutine);
            }

            _offlineTimeRemaining = roundDurationSeconds;
            _offlineTimerCoroutine = StartCoroutine(OfflineTimerRoutine());
            Debug.Log($"[BackendRoundManager] Offline round timer started. reason={reason}, duration={roundDurationSeconds}s");
            return;
        }

        if (Runner == null || !Runner.IsRunning)
        {
            return;
        }

        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDurationSeconds);
        Debug.Log($"[BackendRoundManager] Round timer started. reason={reason}, duration={roundDurationSeconds}s");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcShowRoundDay(int roundNumber)
    {
        RoundDayOverlay.Show(roundNumber);
    }

    private void EndRound(string reason)
    {
        if (isEndingRound)
        {
            return;
        }

        StartCoroutine(EndRoundRoutine(reason));
    }

    private System.Collections.IEnumerator EndRoundRoutine(string reason)
    {
        int completedRoundNumber = CurrentRoundNumber;
        bool shouldPlayDemoThankYouFade = ShouldPlayDemoThankYouFade(completedRoundNumber, reason);

        IsRoundRunning = false;
        HasRoundTimerStarted = false;
        isEndingRound = true;

        if (!AuthSession.IsOffline)
        {
            RoundTimer = TickTimer.None;
        }
        CurrentRoundNumber += 1;

        if (_offlineTimerCoroutine != null)
        {
            StopCoroutine(_offlineTimerCoroutine);
            _offlineTimerCoroutine = null;
        }
        _offlineTimeRemaining = roundDurationSeconds;
        ResetRoundGateStateLocal();
        if (!AuthSession.IsOffline && HasStateAuthority && Runner != null && Runner.IsRunning)
        {
            RpcResetRoundGateState();
        }

        CloseRoundEndBunkerDoors();
        yield return null;

        ApplyRoundEndBunkerPenalties();

        PlayerPrefs.SetInt(_hostRoundCountPrefKey, CurrentRoundNumber);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(_activeHostSlot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();

        if (shouldPlayDemoThankYouFade)
        {
            PlayRoundEndFade();
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, roundEndFadeInSeconds));
        }

        RespawnAllPlayersAtSpawner();
        ResetRoundEndPlayerConditions();
        
        if (AuthSession.IsOffline)
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
        if (shouldPlayDemoThankYouFade)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, roundEndBlackHoldSeconds + roundEndFadeOutSeconds));
        }

        isEndingRound = false;
    }

    private bool ShouldPlayDemoThankYouFade(int completedRoundNumber, string reason)
    {
        return completedRoundNumber == 1 &&
            (reason == "Timeout" || reason == "TimeExpired_Offline");
    }

    private void PlayRoundEndFade()
    {
        if (AuthSession.IsOffline)
        {
            RoundTeleportFade.Play(roundEndFadeInSeconds, roundEndBlackHoldSeconds, roundEndFadeOutSeconds, DemoThankYouMessage);
            return;
        }

        if (Runner != null && Runner.IsRunning)
        {
            RpcPlayRoundEndFade(roundEndFadeInSeconds, roundEndBlackHoldSeconds, roundEndFadeOutSeconds);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayRoundEndFade(float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
    {
        RoundTeleportFade.Play(fadeInSeconds, holdSeconds, fadeOutSeconds, DemoThankYouMessage);
    }

    private void CloseRoundEndBunkerDoors()
    {
        if (AuthSession.IsOffline || Runner == null || !Runner.IsRunning)
        {
            CloseRoundEndBunkerDoorsLocal();
            return;
        }

        if (HasStateAuthority)
        {
            RpcCloseRoundEndBunkerDoors();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcCloseRoundEndBunkerDoors()
    {
        CloseRoundEndBunkerDoorsLocal();
    }

    private void CloseRoundEndBunkerDoorsLocal()
    {
        DoorInteractable[] doors = FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
        foreach (DoorInteractable door in doors)
        {
            if (door == null || !door.IsOpen || !IsBunkerDoor(door.transform))
            {
                continue;
            }

            door.ApplyState(false, 1);
        }
    }

    private bool IsBunkerDoor(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.GetComponent<BunkerExitInteractable>() != null)
            {
                return true;
            }

            if (current.name.IndexOf("bunker", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ApplyRoundEndBunkerPenalties()
    {
        if (AuthSession.IsOffline || Runner == null || !Runner.IsRunning)
        {
            ApplyRoundEndBunkerPenaltyToLocalPlayer();
            return;
        }

        if (HasStateAuthority)
        {
            RpcApplyRoundEndBunkerPenaltyToLocalPlayer();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcApplyRoundEndBunkerPenaltyToLocalPlayer()
    {
        ApplyRoundEndBunkerPenaltyToLocalPlayer();
    }

    private void ApplyRoundEndBunkerPenaltyToLocalPlayer()
    {
        if (!LocalPlayerReferenceResolver.TryGetLocalPlayer(out PlayerController player) || player == null)
        {
            return;
        }

        PlayerRespawn playerRespawn = player.GetComponent<PlayerRespawn>();
        playerRespawn?.ApplyRoundEndBunkerPenaltyIfOutside();
    }

    private void ResetRoundEndPlayerConditions()
    {
        if (AuthSession.IsOffline || Runner == null || !Runner.IsRunning)
        {
            ResetLocalPlayerConditionToDefaults();
            return;
        }

        if (!HasStateAuthority)
        {
            return;
        }

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) || playerObject == null)
            {
                continue;
            }

            playerObject.GetComponent<PlayerCondition>()?.ResetToDefaultValues();
        }

        RpcResetLocalPlayerConditionToDefaults();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcResetLocalPlayerConditionToDefaults()
    {
        ResetLocalPlayerConditionToDefaults();
    }

    private void ResetLocalPlayerConditionToDefaults()
    {
        if (LocalPlayerReferenceResolver.TryGetLocalCondition(out PlayerCondition condition) && condition != null)
        {
            condition.ResetToDefaultValues();
        }
    }

    public void RequestSetWeatherState(NetworkWeatherState weatherState)
    {
        if (AuthSession.IsOffline)
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
        if (!AuthSession.IsOffline && (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid)) return;

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
