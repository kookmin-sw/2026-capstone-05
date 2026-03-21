using UnityEngine;

public class DynamicViewmodelOffset : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform viewmodelContainer;

    [Header("Settings")]
    public Vector3 maxLookDownOffset = new Vector3(0f, 0f, -0.2f);

    private Vector3 defaultLocalPos;

    private void Start()
    {
        if (viewmodelContainer != null)
        {
            defaultLocalPos = viewmodelContainer.localPosition;
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform == null || viewmodelContainer == null) return;

        float pitch = cameraTransform.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        float lookDownFactor = Mathf.Clamp01(pitch / 80f);

        Vector3 targetPos = defaultLocalPos + (maxLookDownOffset * lookDownFactor);
        viewmodelContainer.localPosition = Vector3.Lerp(viewmodelContainer.localPosition, targetPos, Time.deltaTime * 10f);
    }
}