using Fusion;
using Systems.StorageSystem;
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
        if (StorageNetworkSync.Instance != null)
        {
            StorageNetworkSync.Instance.SaveAllStorages();
        }

        Debug.Log($"[BackendRoundManager] 라운드 종료. slot={_activeHostSlot}, round={CurrentRoundNumber}, reason={reason}");
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

}
