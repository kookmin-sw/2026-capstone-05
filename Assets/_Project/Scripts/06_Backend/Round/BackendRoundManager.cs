using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BackendRoundManager : NetworkBehaviour
{
    private const string HostRoundCountPrefKeyPrefix = "06_Backend.HostRoundCount";
    private const string DefaultHostIdentity = "anonymous-host";

    [Header("Round")]
    [SerializeField] private float roundDurationSeconds = 300f;

    [Networked] public NetworkBool IsRoundRunning { get; private set; }
    [Networked] public TickTimer RoundTimer { get; private set; }
    [Networked] public int CurrentRoundNumber { get; private set; }

    private string _hostRoundCountPrefKey = $"{HostRoundCountPrefKeyPrefix}.{DefaultHostIdentity}";

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

        _hostRoundCountPrefKey = BuildHostRoundCountPrefKey();
        CurrentRoundNumber = Mathf.Max(0, PlayerPrefs.GetInt(_hostRoundCountPrefKey, 0));
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
    public void RpcRequestToggleRound(PlayerRef requestedBy)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (IsRoundRunning)
        {
            EndRound($"RequestedBy:{requestedBy}");
            return;
        }

        StartRound($"RequestedBy:{requestedBy}");
    }

    private void StartRound(string reason)
    {
        IsRoundRunning = true;
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDurationSeconds);
        CurrentRoundNumber += 1;

        PlayerPrefs.SetInt(_hostRoundCountPrefKey, CurrentRoundNumber);
        PlayerPrefs.Save();

        Debug.Log($"[BackendRoundManager] 라운드 시작. round={CurrentRoundNumber}, reason={reason}, duration={roundDurationSeconds}s");
    }

    private void EndRound(string reason)
    {
        IsRoundRunning = false;
        RoundTimer = TickTimer.None;

        RespawnAllPlayersAtSpawner();

        Debug.Log($"[BackendRoundManager] 라운드 종료. round={CurrentRoundNumber}, reason={reason}");
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

    private static string BuildHostRoundCountPrefKey()
    {
        string rawIdentity = AuthSession.IsLoggedIn
            ? AuthSession.CurrentUserId
            : DefaultHostIdentity;

        string normalizedIdentity = string.IsNullOrWhiteSpace(rawIdentity)
            ? DefaultHostIdentity
            : rawIdentity.Trim().ToLowerInvariant();

        return $"{HostRoundCountPrefKeyPrefix}.{normalizedIdentity}";
    }
}
