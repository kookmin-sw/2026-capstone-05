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

        HandleInputAndSubStates();
        HandleMovement();
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
    }

    private void HandleInputAndSubStates()
    {
        if (player.InputHandler.JumpTriggered)
        {
            if (currentPosture == PlayerGroundedPosture.Standing)
            {
                player.currentVelocity.y = player.jumpForce;

                player.InputHandler.ConsumeJump();
                stateMachine.ChangeState(player.AirborneState);
                return;
            }
            else if (currentPosture == PlayerGroundedPosture.Crouching) // 앉아있을 때 점프하려고 하면 그냥 일어나기
            {
                if (player.CanStandUp())
                {
                    currentPosture = PlayerGroundedPosture.Standing;
                    player.targetHeight = player.standingHeight;
                }
                else
                {
                    Debug.Log("Can't stand up");
                }
            }
            player.InputHandler.ConsumeJump();
        }

        if (player.InputHandler.CrouchTriggered)
        {
            if (currentPosture == PlayerGroundedPosture.Standing)
            {
                currentPosture = PlayerGroundedPosture.Crouching;
                player.targetHeight = player.crouchHeight;
            }
            else if(currentPosture == PlayerGroundedPosture.Crouching)
            {
                if (player.CanStandUp())
                {
                    currentPosture = PlayerGroundedPosture.Standing;
                    player.targetHeight = player.standingHeight;
                }
                else
                {
                    Debug.Log("Can't stand up");
                }
            }

            player.InputHandler.ConsumeCrouch();
        }

        Vector2 input = player.InputHandler.MoveInput;

        if (input.sqrMagnitude < 0.01f)
        {
            currentLocomotion = PlayerGroundedLocomotion.Idle;
        }
        else if (player.InputHandler.IsSprinting && currentPosture == PlayerGroundedPosture.Standing && input.y > 0)
        {
            currentLocomotion = PlayerGroundedLocomotion.Sprinting;
        }
        else
        {
            currentLocomotion = PlayerGroundedLocomotion.Walking;
        }
    }

    private void HandleMovement()
    {
        Vector2 input = player.InputHandler.MoveInput;

        Vector3 moveDirection = (player.transform.right * input.x + player.transform.forward * input.y).normalized;
        float targetSpeed = 0f;

        if (currentPosture == PlayerGroundedPosture.Crouching)
        {
            targetSpeed = player.crouchSpeed;
        }
        else
        {
            targetSpeed = currentLocomotion == PlayerGroundedLocomotion.Sprinting
                ? player.sprintSpeed
                : player.walkSpeed;
        }

        if (currentLocomotion == PlayerGroundedLocomotion.Idle)
        {
            targetSpeed = 0f;
        }

        player.currentVelocity.x = moveDirection.x * targetSpeed;
        player.currentVelocity.z = moveDirection.z * targetSpeed;
    }
}
