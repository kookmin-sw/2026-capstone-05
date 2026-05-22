using FMODUnity;
using Fusion;
using UnityEngine;
using UnityEngine.Localization.Settings;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BunkerEnterInteractable : NetworkBehaviour, IInteractable
{
    [Header("Interaction Assist")]
    [SerializeField] private bool autoCreateInteractionCollider = true;
    [SerializeField] private float assistColliderRadius = 3.5f;
    [SerializeField] private float maxInteractDistance = 4f;

    [Header("Focus Outline")]
    [SerializeField] private bool autoConfigureOutline = true;
    [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineAll;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 5f;

    [Header("Sound Settings")]
    [SerializeField] private EventReference enterSound;

    private SphereCollider assistCollider;
    private Outline cachedOutline;

    private readonly string objectNameTable = "ObjectNames";
    private readonly string objectNameKey = "Bunker";
    private readonly string interactPromptTable = "InteractPrompts";
    private readonly string interactPromptKey = "EnterBunker";

    public bool CanInteract(PlayerController player)
    {
        if (!enabled || player == null || player.InputHandler == null || !IsRoundRunning())
        {
            return false;
        }

        return Vector3.Distance(player.transform.position, transform.position) <= Mathf.Max(assistColliderRadius, maxInteractDistance);
    }

    public void OnInteract(PlayerController player)
    {
        if (!CanInteract(player))
        {
            return;
        }

        PlayerRef requester = GetPlayerRef(player);
        if (Runner != null)
        {
            RpcRequestTeleportToSpawner(requester);

            RuntimeManager.PlayOneShot(enterSound, player.transform.position);
            return;
        }

        TeleportPlayerToSpawner(player);

        RuntimeManager.PlayOneShot(enterSound, player.transform.position);
    }

    public string GetInteractPrompt()
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, interactPromptKey);
    }

    public string GetObjectName()
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(objectNameTable, objectNameKey);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestTeleportToSpawner(PlayerRef requestedBy)
    {
        if (!HasStateAuthority || Runner == null || requestedBy == PlayerRef.None || !IsRoundRunning())
        {
            return;
        }

        if (!Runner.TryGetPlayerObject(requestedBy, out NetworkObject playerObject) || playerObject == null)
        {
            return;
        }

        TeleportPlayerObjectToSpawner(playerObject);
        RpcSyncPlayerBunkerState(requestedBy, true);
    }

    private void TeleportPlayerToSpawner(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        NetworkObject playerObject = player.GetComponent<NetworkObject>();
        if (playerObject != null)
        {
            TeleportPlayerObjectToSpawner(playerObject);
            return;
        }

        TeleportTransformToSpawner(player.transform, player);
    }

    private void TeleportPlayerObjectToSpawner(NetworkObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        PlayerRespawn playerRespawn = playerObject.GetComponent<PlayerRespawn>();
        if (playerRespawn != null)
        {
            playerRespawn.SpawnAtSpawner();
            SetPlayerInBunker(playerObject.GetComponent<PlayerController>(), true);
            ResetPlayerVelocity(playerObject.GetComponent<PlayerController>());
            return;
        }

        TeleportTransformToSpawner(playerObject.transform, playerObject.GetComponent<PlayerController>());
    }

    private void TeleportTransformToSpawner(Transform playerTransform, PlayerController playerController)
    {
        if (playerTransform == null || Spawner.Instance == null)
        {
            Debug.LogWarning("[BunkerEnterInteractable] PlayerSpawner를 찾을 수 없습니다.");
            return;
        }

        Transform spawnPoint = Spawner.Instance.GetSpawnPoint();
        TeleportTransform(playerTransform, playerController, spawnPoint.position, spawnPoint.rotation);
        SetPlayerInBunker(playerController, true);
    }

    private void TeleportTransform(Transform playerTransform, PlayerController playerController, Vector3 position, Quaternion rotation)
    {
        CharacterController characterController = playerTransform.GetComponent<CharacterController>();

        if (characterController != null && playerController != null)
        {
            Vector3 point1 = playerTransform.position + characterController.center + Vector3.up * (characterController.height * 0.5f - characterController.radius);
            Vector3 point2 = playerTransform.position + characterController.center - Vector3.up * (characterController.height * 0.5f - characterController.radius);

            Collider[] hits = Physics.OverlapCapsule(point1, point2, characterController.radius, Physics.AllLayers, QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                if (hit.isTrigger)
                {
                    var triggerScript = hit.GetComponent<IndoorsTrigger>();
                    if (triggerScript != null)
                    {
                        triggerScript.OnPlayerForceExit(playerController);
                    }
                }
            }
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        playerTransform.SetPositionAndRotation(position, rotation);
        ResetPlayerVelocity(playerController);

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    private void ResetPlayerVelocity(PlayerController playerController)
    {
        if (playerController != null)
        {
            playerController.currentVelocity = Vector3.zero;
        }
    }

    private void SetPlayerInBunker(PlayerController playerController, bool value)
    {
        PlayerCondition condition = playerController != null
            ? playerController.Condition
            : null;

        if (condition == null && playerController != null)
        {
            condition = playerController.GetComponent<PlayerCondition>();
        }

        condition?.SetInBunker(value);
    }

    private PlayerRef GetPlayerRef(PlayerController player)
    {
        NetworkObject playerNetworkObject = player != null ? player.GetComponent<NetworkObject>() : null;
        return playerNetworkObject != null ? playerNetworkObject.InputAuthority : PlayerRef.None;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcSyncPlayerBunkerState(PlayerRef playerRef, NetworkBool isInBunker)
    {
        if (Runner == null || playerRef == PlayerRef.None)
        {
            return;
        }

        if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) || playerObject == null)
        {
            return;
        }

        SetPlayerInBunker(playerObject.GetComponent<PlayerController>(), isInBunker);
    }

    private bool IsRoundRunning()
    {
        return BackendRoundManager.Instance != null && BackendRoundManager.Instance.IsRoundRunning;
    }

    private void Awake()
    {
        if (autoCreateInteractionCollider)
        {
            EnsureAssistCollider();
        }

        if (autoConfigureOutline)
        {
            EnsureOutlineComponent();
        }
    }

    private void OnValidate()
    {
        assistColliderRadius = Mathf.Max(0.5f, assistColliderRadius);
        maxInteractDistance = Mathf.Max(assistColliderRadius, maxInteractDistance);
        outlineWidth = Mathf.Max(0f, outlineWidth);

        if (assistCollider != null)
        {
            assistCollider.radius = assistColliderRadius;
        }

        ApplyOutlineStyle();
    }

    private void EnsureAssistCollider()
    {
        assistCollider = GetComponent<SphereCollider>();
        if (assistCollider == null)
        {
            assistCollider = gameObject.AddComponent<SphereCollider>();
        }

        assistCollider.isTrigger = true;
        assistCollider.radius = assistColliderRadius;
    }

    private void EnsureOutlineComponent()
    {
        cachedOutline = GetComponent<Outline>();
        if (cachedOutline == null)
        {
            cachedOutline = gameObject.AddComponent<Outline>();
        }

        ApplyOutlineStyle();
        cachedOutline.enabled = false;
    }

    private void ApplyOutlineStyle()
    {
        if (cachedOutline == null)
        {
            return;
        }

        cachedOutline.OutlineMode = outlineMode;
        cachedOutline.OutlineColor = outlineColor;
        cachedOutline.OutlineWidth = outlineWidth;
    }
}
