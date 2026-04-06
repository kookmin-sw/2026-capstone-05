using Fusion;
using UnityEngine;

public class PlayerPostureController
{
    private float _standingHeight;
    private float _crouchHeight;
    private float _targetHeight;
    private Vector3 _defaultCenter;
    private float _defaultCameraHeight;
    private bool _isInitialized;

    public void Initialize(CharacterController controller, float crouchHeightRatio)
    {
        _standingHeight = controller.height;
        _crouchHeight = _standingHeight * crouchHeightRatio;
        _targetHeight = _standingHeight;
        _defaultCenter = controller.center;
        _isInitialized = true;
    }

    public void BindCamera(FirstPersonCamera firstPersonCamera)
    {
        if (firstPersonCamera == null)
            return;

        _defaultCameraHeight = firstPersonCamera.CameraHeightOffset;
    }

    public NetworkBool HandleCrouchToggle(
        NetworkInputData inputData,
        NetworkButtons previousButtons,
        NetworkBool isCrouching,
        CharacterController controller,
        Transform playerTransform,
        LayerMask obstacleLayer)
    {
        if (!inputData.Buttons.WasPressed(previousButtons, NetworkInputData.Crouch))
            return isCrouching;

        if (isCrouching)
            return CanStandUp(controller, playerTransform, obstacleLayer) ? false : isCrouching;

        return true;
    }

    public void ApplyTransition(
        CharacterController controller,
        FirstPersonCamera firstPersonCamera,
        bool isCrouching,
        float crouchTransitionSpeed,
        float deltaTime)
    {
        if (!_isInitialized)
            return;

        _targetHeight = isCrouching ? _crouchHeight : _standingHeight;

        controller.height = Mathf.Lerp(controller.height, _targetHeight, deltaTime * crouchTransitionSpeed);
        float centerOffsetY = (controller.height - _standingHeight) * 0.5f;
        controller.center = _defaultCenter + new Vector3(0f, centerOffsetY, 0f);

        if (firstPersonCamera != null)
        {
            float headDropAmount = _standingHeight - controller.height;
            firstPersonCamera.CameraHeightOffset = _defaultCameraHeight - headDropAmount;
        }
    }

    private bool CanStandUp(CharacterController controller, Transform playerTransform, LayerMask obstacleLayer)
    {
        float radius = controller.radius;
        Vector3 point1 = playerTransform.position + Vector3.up * radius;
        Vector3 point2 = playerTransform.position + Vector3.up * (_standingHeight - radius);
        return !Physics.CheckCapsule(point1, point2, radius, obstacleLayer, QueryTriggerInteraction.Ignore);
    }
}
