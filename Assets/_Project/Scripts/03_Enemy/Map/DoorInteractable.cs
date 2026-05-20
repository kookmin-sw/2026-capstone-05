using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DoorInteractable : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<int, DoorInteractable> DoorsByKey = new Dictionary<int, DoorInteractable>();
    private const int SwingSampleCount = 12;
    private const float PlayerClearancePadding = 0.15f;

    [Header("Door")]
    [SerializeField] private Transform doorTransform;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private Vector3 panelLocalDirection = Vector3.right;

    [Header("AI Navigation")]
    [SerializeField] private NavMeshObstacle navMeshObstacle;
    [SerializeField] private bool manageNavMeshObstacle = true;

    [Header("FMOD Events")]
    [SerializeField] private EventReference openEvent;
    [SerializeField] private EventReference closeEvent;

    [Header("Prompt")]
    [SerializeField] private bool refreshPromptAfterInteract = true;

    private Quaternion closedLocalRotation;
    private Quaternion targetLocalRotation;
    private bool isOpen;
    private int currentOpenDirection = 1;
    private int _doorKey;

    public int DoorKey => _doorKey;
    public bool IsOpen => isOpen;

    private readonly string objectNameTable = "ObjectNames";
    private readonly string objectNameKey = "Door";
    private readonly string interactPromptTable = "InteractPrompts";
    private readonly string openPromptKey = "OpenDoor";
    private readonly string closePromptKey = "CloseDoor";

    private void Awake()
    {
        _doorKey = BuildStableDoorKey();
        RegisterDoor();

        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        closedLocalRotation = doorTransform.localRotation;
        targetLocalRotation = closedLocalRotation;
        NormalizePanelDirection();
        ResolveNavMeshObstacle();
        SyncNavMeshObstacle();
    }

    private void OnEnable()
    {
        RegisterDoor();
    }

    private void OnDisable()
    {
        if (DoorsByKey.TryGetValue(_doorKey, out DoorInteractable door) && door == this)
        {
            DoorsByKey.Remove(_doorKey);
        }
    }

    private void Update()
    {
        if (doorTransform == null)
        {
            return;
        }

        if (Quaternion.Angle(doorTransform.localRotation, targetLocalRotation) <= 0.1f)
        {
            doorTransform.localRotation = targetLocalRotation;
            return;
        }

        doorTransform.localRotation = Quaternion.RotateTowards(
            doorTransform.localRotation,
            targetLocalRotation,
            rotationSpeed * Time.deltaTime);
    }

    public bool CanInteract(PlayerController player)
    {
        return enabled && player != null && doorTransform != null;
    }

    public void OnInteract(PlayerController player)
    {
        if (!CanInteract(player))
        {
            return;
        }

        bool shouldOpen = !isOpen;
        if (!shouldOpen && WouldClosingHitPlayer(player))
        {
            return;
        }

        BackendPlayerNetworkSync networkSync = player != null ? player.GetComponent<BackendPlayerNetworkSync>() : null;
        if (networkSync != null && networkSync.IsNetworkReady)
        {
            networkSync.RequestDoorToggle(this, shouldOpen, shouldOpen ? GetOpenDirection(player) : currentOpenDirection);
        }
        else
        {
            ApplyState(shouldOpen, shouldOpen ? GetOpenDirection(player) : currentOpenDirection);
        }
    }

    public string GetInteractPrompt()
    {
        if (isOpen)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, closePromptKey);
        }
        else
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, openPromptKey);
        }
    }

    public string GetObjectName()
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(objectNameTable, objectNameKey);
    }

    public void ApplyState(bool shouldOpen, int openDirection)
    {
        bool stateChanged = isOpen != shouldOpen;

        if (shouldOpen)
        {
            int direction = openDirection >= 0 ? 1 : -1;
            currentOpenDirection = direction;
            targetLocalRotation = closedLocalRotation * Quaternion.Euler(0f, 0f, openAngle * direction);
            isOpen = true;
        }
        else
        {
            targetLocalRotation = closedLocalRotation;
            isOpen = false;
        }

        SyncNavMeshObstacle();
        if (stateChanged)
        {
            PlayDoorSound(shouldOpen);
            NoiseManager.Instance.GenerateNoise(doorTransform.position, NoiseData.NoiseType.Door);
        }

        RefreshInteractionPrompt();
    }

    private void PlayDoorSound(bool opened)
    {
        EventReference eventReference = opened ? openEvent : closeEvent;
        if (eventReference.IsNull)
        {
            return;
        }

        Vector3 soundPosition = doorTransform != null ? doorTransform.position : transform.position;
        RuntimeManager.PlayOneShot(eventReference, soundPosition);
    }

    private void SyncNavMeshObstacle()
    {
        if (!manageNavMeshObstacle)
        {
            return;
        }

        ResolveNavMeshObstacle();
        if (navMeshObstacle == null)
        {
            return;
        }

        navMeshObstacle.carving = true;
        navMeshObstacle.enabled = !isOpen;
    }

    private void ResolveNavMeshObstacle()
    {
        if (navMeshObstacle != null)
        {
            return;
        }

        navMeshObstacle = doorTransform != null
            ? doorTransform.GetComponentInChildren<NavMeshObstacle>()
            : GetComponentInChildren<NavMeshObstacle>();
    }

    private int GetOpenDirection(PlayerController player)
    {
        if (!TryBuildDoorGeometry(out DoorGeometry geometry))
        {
            return 1;
        }

        Vector3 playerCenter = GetPlayerCenter(player);
        Vector3 playerOffset = Vector3.ProjectOnPlane(playerCenter - doorTransform.position, geometry.HingeAxis);
        if (playerOffset.sqrMagnitude < 0.0001f)
        {
            return GetViewFallbackDirection(player);
        }

        float playerRadius = GetPlayerRadius(player);
        float positiveScore = EvaluateSwingClearance(geometry, playerOffset, playerRadius, 1, 0f, openAngle);
        float negativeScore = EvaluateSwingClearance(geometry, playerOffset, playerRadius, -1, 0f, openAngle);

        if (!Mathf.Approximately(positiveScore, negativeScore))
        {
            return positiveScore > negativeScore ? 1 : -1;
        }

        return GetSideBasedDirection(playerOffset, geometry, player);
    }

    private bool WouldClosingHitPlayer(PlayerController player)
    {
        if (!TryBuildDoorGeometry(out DoorGeometry geometry))
        {
            return false;
        }

        Vector3 playerOffset = Vector3.ProjectOnPlane(GetPlayerCenter(player) - doorTransform.position, geometry.HingeAxis);
        float clearance = EvaluateSwingClearance(geometry, playerOffset, GetPlayerRadius(player), currentOpenDirection, openAngle, 0f);
        return clearance < 0f;
    }

    private float EvaluateSwingClearance(
        DoorGeometry geometry,
        Vector3 playerOffset,
        float playerRadius,
        int direction,
        float fromAngle,
        float toAngle)
    {
        float minClearance = float.PositiveInfinity;
        for (int i = 1; i <= SwingSampleCount; i++)
        {
            float t = i / (float)SwingSampleCount;
            float angle = Mathf.Lerp(fromAngle, toAngle, t) * direction;
            Quaternion rotation = Quaternion.AngleAxis(angle, geometry.HingeAxis);
            Vector3 segmentStart = rotation * (geometry.PanelDirection * geometry.MinPanelProjection);
            Vector3 segmentEnd = rotation * (geometry.PanelDirection * geometry.MaxPanelProjection);
            float distance = DistancePointToSegment(playerOffset, segmentStart, segmentEnd);
            minClearance = Mathf.Min(minClearance, distance - playerRadius);
        }

        return minClearance;
    }

    private int GetSideBasedDirection(Vector3 playerOffset, DoorGeometry geometry, PlayerController player)
    {
        float playerSide = Vector3.Dot(playerOffset, geometry.SideNormal);
        if (Mathf.Abs(playerSide) < 0.0001f)
        {
            return GetViewFallbackDirection(player);
        }

        Vector3 positivePanelDirection = Quaternion.AngleAxis(openAngle, geometry.HingeAxis) * geometry.PanelDirection;
        Vector3 negativePanelDirection = Quaternion.AngleAxis(-openAngle, geometry.HingeAxis) * geometry.PanelDirection;
        float positiveOpensTowardPlayer = playerSide * Vector3.Dot(positivePanelDirection, geometry.SideNormal);
        float negativeOpensTowardPlayer = playerSide * Vector3.Dot(negativePanelDirection, geometry.SideNormal);

        if (Mathf.Approximately(positiveOpensTowardPlayer, negativeOpensTowardPlayer))
        {
            return GetViewFallbackDirection(player);
        }

        return positiveOpensTowardPlayer < negativeOpensTowardPlayer ? 1 : -1;
    }

    private int GetViewFallbackDirection(PlayerController player)
    {
        Transform viewTransform = player.CameraTransform != null ? player.CameraTransform : player.transform;
        Vector3 viewForward = viewTransform.forward;
        viewForward.y = 0f;

        if (viewForward.sqrMagnitude < 0.0001f)
        {
            return 1;
        }

        viewForward.Normalize();
        Quaternion parentRotation = doorTransform.parent != null
            ? doorTransform.parent.rotation
            : Quaternion.identity;
        Quaternion positiveWorldRotation = parentRotation * closedLocalRotation * Quaternion.Euler(0f, 0f, openAngle);
        Quaternion negativeWorldRotation = parentRotation * closedLocalRotation * Quaternion.Euler(0f, 0f, -openAngle);
        Vector3 panelDirection = panelLocalDirection.normalized;
        float positiveAlignment = Vector3.Dot((positiveWorldRotation * panelDirection).normalized, viewForward);
        float negativeAlignment = Vector3.Dot((negativeWorldRotation * panelDirection).normalized, viewForward);
        return positiveAlignment >= negativeAlignment ? 1 : -1;
    }

    private bool TryBuildDoorGeometry(out DoorGeometry geometry)
    {
        geometry = default;
        if (doorTransform == null)
        {
            return false;
        }

        Quaternion parentRotation = doorTransform.parent != null ? doorTransform.parent.rotation : Quaternion.identity;
        Quaternion closedWorldRotation = parentRotation * closedLocalRotation;
        Vector3 hingeAxis = closedWorldRotation * Vector3.forward;
        if (hingeAxis.sqrMagnitude < 0.0001f)
        {
            hingeAxis = Vector3.up;
        }

        hingeAxis.Normalize();

        Vector3 panelDirection = Vector3.ProjectOnPlane(closedWorldRotation * panelLocalDirection.normalized, hingeAxis);
        if (panelDirection.sqrMagnitude < 0.0001f)
        {
            panelDirection = Vector3.ProjectOnPlane(doorTransform.right, hingeAxis);
        }

        if (panelDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        panelDirection.Normalize();
        Vector3 alternateDirection = Vector3.Cross(hingeAxis, panelDirection).normalized;

        GetPanelProjectionRange(panelDirection, out float panelMin, out float panelMax);
        GetPanelProjectionRange(alternateDirection, out float alternateMin, out float alternateMax);

        float panelSpan = panelMax - panelMin;
        float alternateSpan = alternateMax - alternateMin;
        if (alternateSpan > panelSpan * 1.25f)
        {
            panelDirection = alternateDirection;
            panelMin = alternateMin;
            panelMax = alternateMax;
        }

        if (panelMax - panelMin < 0.1f)
        {
            panelMin = 0f;
            panelMax = 1f;
        }

        Vector3 sideNormal = Vector3.Cross(hingeAxis, panelDirection);
        if (sideNormal.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        geometry = new DoorGeometry(
            hingeAxis,
            panelDirection,
            sideNormal.normalized,
            panelMin,
            panelMax);
        return true;
    }

    private void GetPanelProjectionRange(Vector3 direction, out float min, out float max)
    {
        min = float.PositiveInfinity;
        max = float.NegativeInfinity;

        Collider[] colliders = doorTransform.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            AccumulateBoundsProjection(collider.bounds, direction, ref min, ref max);
        }

        Renderer[] renderers = doorTransform.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            AccumulateBoundsProjection(renderer.bounds, direction, ref min, ref max);
        }

        if (float.IsInfinity(min) || float.IsInfinity(max))
        {
            min = 0f;
            max = 1f;
        }
    }

    private void AccumulateBoundsProjection(Bounds bounds, Vector3 direction, ref float min, ref float max)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    float projection = Vector3.Dot(corner - doorTransform.position, direction);
                    min = Mathf.Min(min, projection);
                    max = Mathf.Max(max, projection);
                }
            }
        }
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.0001f)
        {
            return Vector3.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
        return Vector3.Distance(point, start + segment * t);
    }

    private static Vector3 GetPlayerCenter(PlayerController player)
    {
        CharacterController controller = player.Controller != null
            ? player.Controller
            : player.GetComponent<CharacterController>();
        return controller != null ? controller.bounds.center : player.transform.position;
    }

    private static float GetPlayerRadius(PlayerController player)
    {
        CharacterController controller = player.Controller != null
            ? player.Controller
            : player.GetComponent<CharacterController>();
        if (controller == null)
        {
            return 0.4f + PlayerClearancePadding;
        }

        float scale = Mathf.Max(Mathf.Abs(player.transform.lossyScale.x), Mathf.Abs(player.transform.lossyScale.z));
        return controller.radius * scale + PlayerClearancePadding;
    }

    private readonly struct DoorGeometry
    {
        public readonly Vector3 HingeAxis;
        public readonly Vector3 PanelDirection;
        public readonly Vector3 SideNormal;
        public readonly float MinPanelProjection;
        public readonly float MaxPanelProjection;

        public DoorGeometry(
            Vector3 hingeAxis,
            Vector3 panelDirection,
            Vector3 sideNormal,
            float minPanelProjection,
            float maxPanelProjection)
        {
            HingeAxis = hingeAxis;
            PanelDirection = panelDirection;
            SideNormal = sideNormal;
            MinPanelProjection = minPanelProjection;
            MaxPanelProjection = maxPanelProjection;
        }
    }

    private void RefreshInteractionPrompt()
    {
        if (!refreshPromptAfterInteract || InteractionUI.Instance == null)
        {
            return;
        }

        InteractionUI.Instance.RefreshPromptForTarget(doorTransform, GetObjectName(), GetInteractPrompt());
    }

    private void NormalizePanelDirection()
    {
        if (panelLocalDirection.sqrMagnitude < 0.0001f)
        {
            panelLocalDirection = Vector3.right;
        }
    }

    private void OnValidate()
    {
        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        openAngle = Mathf.Clamp(openAngle, 0f, 180f);
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
        NormalizePanelDirection();
    }

    public static bool TryGetDoor(int doorKey, out DoorInteractable door)
    {
        return DoorsByKey.TryGetValue(doorKey, out door) && door != null;
    }

    public static void ApplyNetworkState(int doorKey, bool shouldOpen, int openDirection)
    {
        if (TryGetDoor(doorKey, out DoorInteractable door))
        {
            door.ApplyState(shouldOpen, openDirection);
        }
    }

    private void RegisterDoor()
    {
        if (_doorKey == 0)
        {
            _doorKey = BuildStableDoorKey();
        }

        DoorsByKey[_doorKey] = this;
    }

    private int BuildStableDoorKey()
    {
        unchecked
        {
            int hash = 17;
            Scene scene = gameObject.scene;
            hash = hash * 31 + StableStringHash(scene.path);
            hash = hash * 31 + StableStringHash(BuildHierarchyPath(transform));
            hash = hash * 31 + Mathf.RoundToInt(transform.position.x * 100f);
            hash = hash * 31 + Mathf.RoundToInt(transform.position.y * 100f);
            hash = hash * 31 + Mathf.RoundToInt(transform.position.z * 100f);
            return hash == 0 ? 1 : hash;
        }
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
            return string.Empty;

        string path = target.name;
        Transform parent = target.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private static int StableStringHash(string value)
    {
        unchecked
        {
            int hash = (int)2166136261;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }
            }

            return hash;
        }
    }
}
