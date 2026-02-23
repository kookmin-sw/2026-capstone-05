using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonCamera : MonoBehaviour
{
    public Transform Target;
    public float MouseSensitivity = 0.002f;

    private float verticalRotation;
    private float horizontalRotation;

    void LateUpdate()
    {
        if (Target == null) return;

        transform.position = Target.position;

        Vector2 delta = Vector2.zero;
        if (Mouse.current != null)
            delta = Mouse.current.delta.ReadValue();

        horizontalRotation += delta.x * MouseSensitivity * Time.deltaTime * 60f;
        verticalRotation -= delta.y * MouseSensitivity * Time.deltaTime * 60f;

        verticalRotation = Mathf.Clamp(verticalRotation, -70f, 70f);

        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0);
    }
}
