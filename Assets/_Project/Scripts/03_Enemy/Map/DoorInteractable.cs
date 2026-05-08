using UnityEngine;

[DisallowMultipleComponent]
public class DoorInteractable : MonoBehaviour, IInteractable
{
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

    private void Awake()
    {
        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        closedLocalRotation = doorTransform.localRotation;
        targetLocalRotation = closedLocalRotation;
        NormalizePanelDirection();
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

        if (isOpen)
        {
            CloseDoor();
        }
        else
        {
            OpenDoor(player);
        }

        RefreshInteractionPrompt();
    }

    public string GetInteractPrompt()
    {
        return isOpen ? closePrompt : openPrompt;
    }

    public string GetObjectName()
    {
        return objectName;
    }

    private void OpenDoor(PlayerController player)
    {
        int direction = GetOpenDirection(player);
        targetLocalRotation = closedLocalRotation * Quaternion.Euler(0f, 0f, openAngle * direction);
        isOpen = true;
    }

    private void CloseDoor()
    {
        targetLocalRotation = closedLocalRotation;
        isOpen = false;
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

        InteractionUI.Instance.Show(GetObjectName(), GetInteractPrompt(), doorTransform);
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
}
