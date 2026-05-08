using System.Collections.Generic;
using Fusion;
using Systems.GridInventory;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BackendRoundManager : NetworkBehaviour
{
    private const string HostSelectedSlotPrefKey = "HostSaveSlot";

    [Header("Round")]
    [SerializeField] private float roundDurationSeconds = 300f;

    [Networked] public NetworkBool IsRoundRunning { get; private set; }
    [Networked] public TickTimer RoundTimer { get; private set; }
    [Networked] public int CurrentRoundNumber { get; private set; }

    private string _hostRoundCountPrefKey = RoomLauncher.BuildHostRoundCountPrefKey(1);
    private int _activeHostSlot = 1;
    private readonly HashSet<int> _loadedPlayerRefs = new HashSet<int>();

    public float RoundTimeRemainingSeconds
    {
        get
        {
            if (!IsRoundRunning || Runner == null)
            {
                return 0f;
            }

            return RoundTimer.RemainingTime(Runner) ?? 0f;
        }
    }

    public override void Spawned()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        _activeHostSlot = Mathf.Clamp(PlayerPrefs.GetInt(HostSelectedSlotPrefKey, 1), 1, 3);
        _hostRoundCountPrefKey = RoomLauncher.BuildHostRoundCountPrefKey(_activeHostSlot);
        CurrentRoundNumber = Mathf.Max(1, PlayerPrefs.GetInt(_hostRoundCountPrefKey, 1));
        IsRoundRunning = false;
        RoundTimer = TickTimer.None;

        Debug.Log($"[BackendRoundManager] 호스트 라운드 저장소 로드. key={_hostRoundCountPrefKey}, userId={AuthSession.CurrentUserId}, round={CurrentRoundNumber}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        TryRestoreJoinedPlayerInventories();

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

        if (IsRoundRunning)
        {
            EndRound($"RequestedBy:{requestedBy}");
        }
    }

    private void StartRound(string reason)
    {
        IsRoundRunning = true;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDurationSeconds);
        CurrentRoundNumber += 1;

        PlayerPrefs.SetInt(_hostRoundCountPrefKey, CurrentRoundNumber);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(_activeHostSlot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();

        Debug.Log($"[BackendRoundManager] 라운드 시작. round={CurrentRoundNumber}, reason={reason}, duration={roundDurationSeconds}s");
    }

    private void EndRound(string reason)
    {
        IsRoundRunning = false;
        RoundTimer = TickTimer.None;


        PlayerPrefs.SetInt(_hostRoundCountPrefKey, CurrentRoundNumber);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(_activeHostSlot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();

        RespawnAllPlayersAtSpawner();

        Debug.Log($"[BackendRoundManager] 라운드 종료. slot={_activeHostSlot}, round={CurrentRoundNumber}, reason={reason}");
    }

    private void TryRestoreJoinedPlayerInventories()
    {
        if (Runner == null)
        {
            return;
        }

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            int rawRef = playerRef.RawEncoded;
            if (_loadedPlayerRefs.Contains(rawRef))
            {
                continue;
            }

            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) || playerObject == null)
            {
                continue;
            }

            GridInventory inventory = playerObject.GetComponentInChildren<GridInventory>(true);
            if (inventory == null || inventory.Model == null)
            {
                continue;
            }

            _loadedPlayerRefs.Add(rawRef);
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

            PlayerRespawn playerRespawn = playerObject.GetComponent<PlayerRespawn>();
            if (playerRespawn != null)
            {
                playerRespawn.SpawnAtSpawner();
                continue;
            }

            if (Spawner.Instance == null)
            {
                continue;
            }

            Transform spawnPoint = Spawner.Instance.GetSpawnPoint();
            playerObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }
    }

    public static void ResetHostSlotRoundToOne(int slot)
    {
        int safeSlot = Mathf.Clamp(slot, 1, 3);
        string key = RoomLauncher.BuildHostRoundCountPrefKey(safeSlot);
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.SetString(RoomLauncher.BuildHostSaveDatePrefKey(safeSlot), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();
    }

}
