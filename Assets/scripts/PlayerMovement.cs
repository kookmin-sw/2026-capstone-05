using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    private Vector3 _velocity;
    private bool _jumpPressed;

    private CharacterController _controller;

    public float PlayerSpeed = 2f;

    public float JumpForce = 5f;
    public float GravityValue = -9.81f;

    public Camera Camera;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            _jumpPressed = true;
        }
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            _jumpPressed = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Shared 모드에서 "내 플레이어"만 입력/카메라 기반 이동 처리
        if (!HasInputAuthority)
            return;

        if (Camera == null)
        {
            Camera = Camera.main;       //Spawned보다 먼저 호출되는 경우 대비
            if (Camera == null) return; 
        }

        if (_controller.isGrounded)
            _velocity = new Vector3(0, -1, 0);

        Vector2 input = ReadMoveInput();

        Quaternion cameraRotationY = Quaternion.Euler(0, Camera.transform.eulerAngles.y, 0);
        Vector3 move = cameraRotationY * new Vector3(input.x, 0, input.y) * Runner.DeltaTime * PlayerSpeed;

        _velocity.y += GravityValue * Runner.DeltaTime;

        if (_jumpPressed && _controller.isGrounded)
            _velocity.y += JumpForce;

        _controller.Move(move + _velocity * Runner.DeltaTime);

        if (move != Vector3.zero)
            transform.forward = move.normalized;

        _jumpPressed = false;
    }


    private Vector2 ReadMoveInput()
    {
        Vector2 v = Vector2.zero;

        // 키보드 WASD + 방향키
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) v.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) v.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v.y += 1f;
        }

        // 게임패드 왼쪽 스틱
        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.01f)
            {
                v += stick;
            }
        }

        return Vector2.ClampMagnitude(v, 1f);
    }

    public override void Spawned()
    {
        Debug.Log($"Spawned - InputAuth:{HasInputAuthority}, StateAuth:{HasStateAuthority}, Local:{Object.InputAuthority} / {Object.StateAuthority}");

        if (HasInputAuthority)
        {
            Camera = Camera.main;
            Camera.GetComponent<FirstPersonCamera>().Target = transform;
        }
    }

}
