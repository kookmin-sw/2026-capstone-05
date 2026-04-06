using Fusion;
using UnityEngine;

/// <summary>
/// 06_Backend 연결 계층의 진입점.
/// 02_Player의 기존 입력/카메라/동작 로직은 수정하지 않고,
/// authority 판정 + 입력 브리지 + 카메라 바인딩 + 1P/3P 전환 순서를 조정한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerAdapter : NetworkBehaviour
{
    private const string LogAdapter = "[PlayerAdapter]";
    private const string LogFlow = "[BackendFlow]";

    [Header("Direct Sync Test")]
    [SerializeField] private bool enableDirectClientSyncTest;
    [SerializeField, Min(1)] private int directSyncSendIntervalTicks = 1;
    [SerializeField, Min(0f)] private float clientRenderLerpSpeed = 20f;

    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }
    [Networked] private NetworkBool HasSyncedTransform { get; set; }
    [Networked] private Vector2 SyncedMoveInput { get; set; }
    [Networked] private Vector2 SyncedLookInput { get; set; }
    [Networked] private NetworkBool SyncedJumpPressed { get; set; }
    [Networked] private NetworkBool SyncedSprintPressed { get; set; }

    private LocalPlayerBinder _binder;

    public override void Spawned()
    {
        DisableLegacyNetworkSetup();

        _binder = GetOrAdd<LocalPlayerBinder>();

        NetworkInputBridge inputBridge = GetOrAdd<NetworkInputBridge>();
        LocalCameraBinder cameraBinder = GetOrAdd<LocalCameraBinder>();
        PlayerPresentationController presentationController = GetOrAdd<PlayerPresentationController>();
        AuthorityStateController authorityController = GetOrAdd<AuthorityStateController>();

        _binder.Initialize(
            Runner,
            Object,
            inputBridge,
            cameraBinder,
            presentationController,
            authorityController);
        authorityController.SetAllowLocalMovementOverride(enableDirectClientSyncTest);

        Debug.Log($"{LogAdapter} Spawned. object={name}, inputAuth={Object.HasInputAuthority}, stateAuth={Object.HasStateAuthority}");

        if (Object.HasStateAuthority)
        {
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
            HasSyncedTransform = true;
        }

        _binder.Apply(force: true);
    }

    public override void Render()
    {
        _binder?.Apply(force: false);

        if (Object == null || Object.HasStateAuthority || Object.HasInputAuthority || !HasSyncedTransform)
            return;

        float lerpT = Mathf.Clamp01(clientRenderLerpSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, SyncedPosition, lerpT);
        transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, lerpT);

        BackendNetworkInputData replicatedInput = new BackendNetworkInputData
        {
            Move = SyncedMoveInput,
            Look = SyncedLookInput,
            JumpPressed = SyncedJumpPressed,
            SprintPressed = SyncedSprintPressed
        };
        _binder.ApplyReplicatedInput(replicatedInput);
    }

    public override void FixedUpdateNetwork()
    {
        if (_binder == null)
            return;

        if (enableDirectClientSyncTest)
            TrySendDirectSyncToServer();

        if (GetInput(out BackendNetworkInputData inputData))
        {
            _binder.ApplyNetworkInput(inputData);

            if (Object.HasStateAuthority)
            {
                SyncedMoveInput = inputData.Move;
                SyncedLookInput = inputData.Look;
                SyncedJumpPressed = inputData.JumpPressed;
                SyncedSprintPressed = inputData.SprintPressed;
            }
        }
        else if (Object.HasStateAuthority)
        {
            SyncedMoveInput = Vector2.zero;
            SyncedLookInput = Vector2.zero;
            SyncedJumpPressed = false;
            SyncedSprintPressed = false;
        }

        if (Object.HasStateAuthority)
        {
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
            HasSyncedTransform = true;
        }

        if (Object.HasStateAuthority)
        {
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
            HasSyncedTransform = true;
        }

        if (Object.HasStateAuthority && Runner != null && Runner.Tick % 20 == 0)
        {
            Vector3 pos = transform.position;
            Vector3 euler = transform.eulerAngles;
            Debug.Log($"{LogFlow} Server propagate. tick={Runner.Tick}, object={name}, pos={pos}, yaw={euler.y:0.00}");
        }
    }

    private T GetOrAdd<T>() where T : Component
    {
        T existing = GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    private void DisableLegacyNetworkSetup()
    {
        PlayerNetworkSetup setup = GetComponent<PlayerNetworkSetup>();
        if (setup == null)
            return;

        setup.enabled = false;
        Debug.LogWarning($"{LogAdapter} Disabled legacy PlayerNetworkSetup on spawned network player. object={name}");
    }

    private void TrySendDirectSyncToServer()
    {
        if (!Object.HasInputAuthority || Object.HasStateAuthority || Runner == null)
            return;

        int interval = Mathf.Max(1, directSyncSendIntervalTicks);
        if (Runner.Tick % interval != 0)
            return;

        Vector3 worldPosition = transform.position;
        Quaternion worldRotation = transform.rotation;
        RpcDirectSync(worldPosition, worldRotation);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
    private void RpcDirectSync(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (!enableDirectClientSyncTest || !Object.HasStateAuthority)
            return;

        transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (Runner != null && Runner.Tick % 20 == 0)
        {
            Vector3 euler = worldRotation.eulerAngles;
            Debug.Log($"{LogFlow} Direct sync applied(server). tick={Runner.Tick}, object={name}, pos={worldPosition}, yaw={euler.y:0.00}");
        }
    }
}
