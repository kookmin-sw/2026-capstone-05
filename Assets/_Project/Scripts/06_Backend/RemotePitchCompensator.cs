using UnityEngine;

/// <summary>
/// 입력 authority는 있지만 state authority가 없는 클라이언트에서
/// 카메라 Pitch(X)를 로컬 반영해 "Yaw만 회전" 문제를 보정한다.
/// </summary>
public class RemotePitchCompensator : MonoBehaviour
{
    private PlayerController _playerController;
    private PlayerInputHandler _inputHandler;
    private bool _active;
    private float _pitch;
    private float _yaw;

    public void Configure(bool active, PlayerController playerController, PlayerInputHandler inputHandler)
    {
        _active = active;
        _playerController = playerController;
        _inputHandler = inputHandler;

        if (_playerController != null && _playerController.CameraTransform != null)
        {
            Vector3 euler = _playerController.CameraTransform.localEulerAngles;
            _pitch = euler.x;
            _yaw = euler.y;
        }

        enabled = active;
    }

    private void Update()
    {
        if (!_active || _playerController == null || _inputHandler == null || _playerController.CameraTransform == null)
            return;

        float lookY = _inputHandler.LookInput.y * _playerController.mouseSensitivity;
        float lookX = _inputHandler.LookInput.x * _playerController.mouseSensitivity;
        _pitch -= lookY;
        _pitch = ClampAngle(_pitch, -_playerController.upDownRange, _playerController.upDownRange);

        if (!_playerController.AllowMovementSimulation)
            _yaw += lookX;
        else
            _yaw = 0f;

        _playerController.CameraTransform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return Mathf.Clamp(angle, min, max);
    }
}
