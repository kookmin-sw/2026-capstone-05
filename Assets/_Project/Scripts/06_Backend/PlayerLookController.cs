using UnityEngine;

public class PlayerLookController
{
    public float ApplyLook(Transform playerTransform, Vector2 lookInput, float currentVerticalRotation, float upDownRange)
    {
        playerTransform.Rotate(0f, lookInput.x, 0f);

        currentVerticalRotation += lookInput.y;
        currentVerticalRotation = Mathf.Clamp(currentVerticalRotation, -upDownRange, upDownRange);

        return currentVerticalRotation;
    }
}
