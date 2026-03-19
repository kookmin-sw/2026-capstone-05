using UnityEngine;

public enum PlayerGroundedPosture
{
    Standing,
    Crouching
}
public enum PlayerGroundedLocomotion
{
    Idle,
    Walking,
    Sprinting
}

public class PlayerGroundedState : PlayerState
{
    private PlayerGroundedPosture currentPosture;
    private PlayerGroundedLocomotion currentLocomotion;

    public PlayerGroundedState(PlayerController player, PlayerStateMachine stateMachine)
        : base(player, stateMachine)
    {

    }

    public override void Enter()
    {
        base.Enter();

        player.useGravity = true;
        player.canLook = true;

        currentPosture = PlayerGroundedPosture.Standing;
        currentLocomotion = PlayerGroundedLocomotion.Idle;
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        if (!player.IsGrounded)
        {
            stateMachine.ChangeState(player.AirborneState);
            return;
        }

        HandleSubStates();
        HandleMovement();
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
    }

    private void HandleSubStates()
    {
        switch (currentPosture)
        {
            case PlayerGroundedPosture.Standing:
                HandleStandingState();
                break;
            case PlayerGroundedPosture.Crouching:
                HandleCrouchingState();
                break;
        }

        UpdateLocomotionState();
    }

    private void HandleStandingState()
    {
        if (player.InputHandler.JumpTriggered)
        {
            ExecuteJump();
            return;
        }

        if (player.InputHandler.CrouchTriggered)
        {
            currentPosture = PlayerGroundedPosture.Crouching;
            player.TargetHeight = player.CrouchHeight;
            player.InputHandler.ConsumeCrouch();
        }
    }

    private void HandleCrouchingState()
    {
        if (player.InputHandler.JumpTriggered || player.InputHandler.CrouchTriggered)
        {
            if (player.CanStandUp())
            {
                currentPosture = PlayerGroundedPosture.Standing;
                player.TargetHeight = player.StandingHeight;
            }
            else
            {
                Debug.Log("Can't stand up");
            }

            player.InputHandler.ConsumeJump();
            player.InputHandler.ConsumeCrouch();
        }
    }

    private void ExecuteJump()
    {
        player.currentVelocity.y = player.jumpForce;
        player.InputHandler.ConsumeJump();
        stateMachine.ChangeState(player.AirborneState);
    }

    private void UpdateLocomotionState()
    {
        Vector2 input = player.InputHandler.MoveInput;

        if (input.sqrMagnitude < 0.01f)
        {
            currentLocomotion = PlayerGroundedLocomotion.Idle;
            return;
        }

        bool isSprinting = currentPosture == PlayerGroundedPosture.Standing &&
                         input.y > 0 &&
                         player.InputHandler.IsSprinting;

        currentLocomotion = isSprinting ? PlayerGroundedLocomotion.Sprinting : PlayerGroundedLocomotion.Walking;
    }

    private void HandleMovement()
    {
        Vector2 input = player.InputHandler.MoveInput;
        Vector3 moveDirection = (player.transform.right * input.x + player.transform.forward * input.y).normalized;

        float targetSpeed = GetTargetSpeed();

        player.currentVelocity.x = moveDirection.x * targetSpeed;
        player.currentVelocity.z = moveDirection.z * targetSpeed;
    }

    private float GetTargetSpeed()
    {
        if (currentLocomotion == PlayerGroundedLocomotion.Idle)
        {
            return 0f;
        }

        switch (currentPosture)
        {
            case PlayerGroundedPosture.Crouching:
                return player.crouchSpeed;

            case PlayerGroundedPosture.Standing:
                return currentLocomotion == PlayerGroundedLocomotion.Sprinting ? player.sprintSpeed : player.walkSpeed;

            default:
                return 0f;
        }
    }
}
