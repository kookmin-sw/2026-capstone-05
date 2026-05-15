using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DoorInteractable : MonoBehaviour, IInteractable
{
    private static readonly Dictionary<int, DoorInteractable> DoorsByKey = new Dictionary<int, DoorInteractable>();

    [Header("Door")]
    [SerializeField] private Transform doorTransform;
    [SerializeField] private string objectName = "문";
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private Vector3 panelLocalDirection = Vector3.right;

    [Header("Prompt")]
    [SerializeField] private string openPrompt = "[E] 열기";
    [SerializeField] private string closePrompt = "[E] 닫기";
    [SerializeField] private bool refreshPromptAfterInteract = true;

    private Quaternion closedLocalRotation;
    private Quaternion targetLocalRotation;
    private bool isOpen;
    private int _doorKey;

    public int DoorKey => _doorKey;
    public bool IsOpen => isOpen;

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

        BackendPlayerNetworkSync networkSync = player != null ? player.GetComponent<BackendPlayerNetworkSync>() : null;
        if (networkSync != null && networkSync.IsNetworkReady)
        {
            networkSync.RequestDoorToggle(this, !isOpen, GetOpenDirection(player));
        }
        else
        {
            ApplyState(!isOpen, GetOpenDirection(player));
        }
    }

    public string GetInteractPrompt()
    {
        return isOpen ? closePrompt : openPrompt;
    }

    public string GetObjectName()
    {
        return objectName;
    }

    public void ApplyState(bool shouldOpen, int openDirection)
    {
        if (shouldOpen)
        {
            int direction = openDirection >= 0 ? 1 : -1;
            targetLocalRotation = closedLocalRotation * Quaternion.Euler(0f, 0f, openAngle * direction);
            isOpen = true;
        }
        else
        {
            targetLocalRotation = closedLocalRotation;
            isOpen = false;
        }

        RefreshInteractionPrompt();
    }

    private int GetOpenDirection(PlayerController player)
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
