using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    public Transform Target;
    public float CameraHeightOffset = 1.6f;

    [Header("Legacy (kept for scene/prefab compatibility)")]
    public float MouseSensitivity = 2f;

    private float _pitch;

    public void SetPitch(float pitch)
    {
        _pitch = pitch;
    }

    private void LateUpdate()
    {
        if (Target == null) return;

        if (transform.parent != Target)
            transform.SetParent(Target, false);

        transform.localPosition = new Vector3(0f, CameraHeightOffset, 0f);
        transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}
