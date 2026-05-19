using Fusion;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BunkerExitInteractable : NetworkBehaviour, IInteractable
{
    [Header("Exit Target")]
    [SerializeField] private Transform baseTransform;
    [SerializeField] private Transform bunkerDoor;
    [SerializeField] private string baseObjectName = "Base";
    [FormerlySerializedAs("bunkerDoorName")]
    [SerializeField] private string bunkerDoorPath = "bunker_scene/Bunker_door";
    [SerializeField] private Vector3 exitWorldOffset = Vector3.zero;
    [SerializeField] private float exitGroundClearance = 0.05f;

    [Header("Interaction Assist")]
    [SerializeField] private bool autoCreateInteractionCollider = true;
    [SerializeField] private float assistColliderRadius = 3.5f;
    [SerializeField] private float maxInteractDistance = 4f;

    [Header("Focus Outline")]
    [SerializeField] private bool autoConfigureOutline = true;
    [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineAll;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 5f;

    private SphereCollider assistCollider;
    private Outline cachedOutline;

    private readonly string objectNameTable = "ObjectNames";
    private readonly string objectNameKey = "Bunker";
    private readonly string interactPromptTable = "InteractPrompts";
    private readonly string interactPromptKey = "ExitBunker";

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
            RpcRequestTeleportToBunkerDoor(requester);
            return;
        }

        TeleportPlayerToBunkerDoor(player);
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
    public void RpcRequestTeleportToBunkerDoor(PlayerRef requestedBy)
    {
        if (!HasStateAuthority || Runner == null || requestedBy == PlayerRef.None || !IsRoundRunning())
        {
            return;
        }

        if (!Runner.TryGetPlayerObject(requestedBy, out NetworkObject playerObject) || playerObject == null)
        {
            return;
        }

        TeleportPlayerObjectToBunkerDoor(playerObject);
    }

    private void TeleportPlayerToBunkerDoor(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        NetworkObject playerObject = player.GetComponent<NetworkObject>();
        if (playerObject != null)
        {
            TeleportPlayerObjectToBunkerDoor(playerObject);
            return;
        }

        TeleportTransformToBunkerDoor(player.transform, player);
    }

    private void TeleportPlayerObjectToBunkerDoor(NetworkObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        TeleportTransformToBunkerDoor(playerObject.transform, playerObject.GetComponent<PlayerController>());
    }

    private void TeleportTransformToBunkerDoor(Transform playerTransform, PlayerController playerController)
    {
        if (playerTransform == null || !TryGetExitPose(out Vector3 position, out Quaternion rotation))
        {
            Debug.LogWarning("[BunkerExitInteractable] Base 아래의 bunker_scene/Bunker_door를 찾을 수 없습니다.");
            return;
        }

        TeleportTransform(playerTransform, playerController, position, rotation);
    }

    private bool TryGetExitPose(out Vector3 position, out Quaternion rotation)
    {
        Transform door = ResolveBunkerDoor();
        if (door == null)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        rotation = GetUprightRotation(door.rotation);

        position = GetDoorTopPosition(door) + exitWorldOffset;
        return true;
    }

    private Transform ResolveBunkerDoor()
    {
        if (bunkerDoor != null)
        {
            return bunkerDoor;
        }

        if (baseTransform == null && !string.IsNullOrEmpty(baseObjectName))
        {
            GameObject baseObject = GameObject.Find(baseObjectName);
            if (baseObject != null)
            {
                baseTransform = baseObject.transform;
            }
        }

        if (baseTransform == null || string.IsNullOrEmpty(bunkerDoorPath))
        {
            return null;
        }

        bunkerDoor = baseTransform.Find(bunkerDoorPath);
        return bunkerDoor;
    }

    private Vector3 GetDoorTopPosition(Transform door)
    {
        if (TryGetWorldBounds(door, out Bounds bounds))
        {
            return new Vector3(bounds.center.x, bounds.max.y + exitGroundClearance, bounds.center.z);
        }

        return door.position + Vector3.up * exitGroundClearance;
    }

    private bool TryGetWorldBounds(Transform target, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(collider.bounds);
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
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

        if (playerController != null)
        {
            playerController.currentVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    private PlayerRef GetPlayerRef(PlayerController player)
    {
        NetworkObject playerNetworkObject = player != null ? player.GetComponent<NetworkObject>() : null;
        return playerNetworkObject != null ? playerNetworkObject.InputAuthority : PlayerRef.None;
    }

    private bool IsRoundRunning()
    {
        return BackendRoundManager.Instance != null && BackendRoundManager.Instance.IsRoundRunning;
    }

    private Quaternion GetUprightRotation(Quaternion sourceRotation)
    {
        return Quaternion.Euler(0f, sourceRotation.eulerAngles.y, 0f);
    }

    private void Awake()
    {
        ResolveBunkerDoor();

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
        exitGroundClearance = Mathf.Max(0f, exitGroundClearance);
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
