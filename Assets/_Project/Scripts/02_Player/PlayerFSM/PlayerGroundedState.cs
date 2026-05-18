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

    public PlayerGroundedPosture CurrentPosture => currentPosture;
    public PlayerGroundedLocomotion CurrentLocomotion => currentLocomotion;

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

        player.Animator.SetGrounded(true);
    }

    public override void Exit()
    {
        base.Exit();

        player.Animator.SetGrounded(false);
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
        HandleAnimation();
        HandleStamina();
        HandleSatiety();
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
            if (player.Condition.UseStamina(player.jumpStaminaCost))
            {
                ExecuteJump();
                return;
            }
            player.InputHandler.ConsumeJump();
        }

        if (player.InputHandler.CrouchTriggered)
        {
            ChangePosture(PlayerGroundedPosture.Crouching);
            player.InputHandler.ConsumeCrouch();
        }
    }

    private void HandleCrouchingState()
    {
        if (player.InputHandler.JumpTriggered || player.InputHandler.CrouchTriggered)
        {
            if (player.CanStandUp())
            {
                ChangePosture(PlayerGroundedPosture.Standing);
            }
            else
            {
                Debug.Log("Can't stand up");
            }

            player.InputHandler.ConsumeJump();
            player.InputHandler.ConsumeCrouch();
        }
    }

    private void ChangePosture(PlayerGroundedPosture newPosture)
    {
        if (currentPosture == newPosture)
        {
            return;
        }

        currentPosture = newPosture;
        player.TargetHeight = newPosture == PlayerGroundedPosture.Standing ? player.StandingHeight : player.CrouchHeight;
        player.Animator.SetCrouching(newPosture == PlayerGroundedPosture.Crouching);
    }

    private void ChangeLocomotion(PlayerGroundedLocomotion newLocomotion)
    {
        if (currentLocomotion == newLocomotion)
        {
            return;
        }

        currentLocomotion = newLocomotion;
        player.Animator.SetSprinting(currentLocomotion == PlayerGroundedLocomotion.Sprinting);
        if (currentLocomotion == PlayerGroundedLocomotion.Sprinting)
        {
            player.Equipment.CancelCurrentItemUse();
        }
    }

    private void ExecuteJump()
    {
        player.currentVelocity.y = player.jumpForce;
        player.NoiseEmitter.GenerateJumpNoise();
        player.InputHandler.ConsumeJump();
        stateMachine.ChangeState(player.AirborneState);
    }

    private void UpdateLocomotionState()
    {
        Vector2 input = player.InputHandler.MoveInput;

        if (input.sqrMagnitude < 0.01f)
        {
            ChangeLocomotion(PlayerGroundedLocomotion.Idle);
            return;
        }

        bool wantsToSprint = currentPosture == PlayerGroundedPosture.Standing &&
                             input.y > 0 &&
                             player.InputHandler.IsSprinting;
        bool canSprint = false;

        if (currentLocomotion == PlayerGroundedLocomotion.Sprinting)
        {
            canSprint = wantsToSprint && player.Condition.stamina.currentValue > 0f;
        }
        else
        {
            canSprint = wantsToSprint && player.Condition.stamina.currentValue >= player.minStaminaToSprint;
        }

        ChangeLocomotion(canSprint ? PlayerGroundedLocomotion.Sprinting : PlayerGroundedLocomotion.Walking);
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

        float inputMagnitude = player.InputHandler.MoveInput.magnitude;
        switch (currentPosture)
        {
            case PlayerGroundedPosture.Crouching:
                return player.crouchSpeed * inputMagnitude;

            case PlayerGroundedPosture.Standing:
                return currentLocomotion == PlayerGroundedLocomotion.Sprinting ? player.sprintSpeed : player.walkSpeed * inputMagnitude;

            default:
                return 0f;
        }
    }

    private void HandleAnimation()
    {
        if (currentLocomotion == PlayerGroundedLocomotion.Idle)
        {
            player.Animator.UpdateMovement(Vector2.zero);
        }
        else
        {
            player.Animator.UpdateMovement(player.InputHandler.MoveInput);
        }
    }

    private void HandleStamina()
    {
        if (currentLocomotion == PlayerGroundedLocomotion.Sprinting)
        {   // 달리는 중
            player.Condition.DrainStamina(player.sprintStaminaCost * Time.deltaTime);

            if (player.Condition.stamina.currentValue <= 0f)
            {
                ChangeLocomotion(PlayerGroundedLocomotion.Walking);
            }
        }
        else
        {
            float targetRegenRate = 0f;

            if (currentLocomotion == PlayerGroundedLocomotion.Idle)
            {   // 가만히 있는 중
                targetRegenRate = player.staminaIdleRegenRate;
            }
            else
            {
                if (currentPosture == PlayerGroundedPosture.Standing)
                {   // 걷는 중
                    targetRegenRate = player.staminaWalkRegenRate;
                }
                else
                {   // 웅크리고 걷는 중
                    targetRegenRate = player.staminaCrouchWalkRegenRate;
                }
            }

            player.Condition.stamina.increaseRate = targetRegenRate;
        }
    }

    private void HandleSatiety()
    {
        float targetSatietyDecay = 0f;
        if (currentLocomotion == PlayerGroundedLocomotion.Idle)
        {
            targetSatietyDecay = player.satietyIdleDecayRate;
        }
        else if (currentLocomotion == PlayerGroundedLocomotion.Walking)
        {
            targetSatietyDecay = currentPosture == PlayerGroundedPosture.Standing ? player.satietyWalkDecayRate : player.satietyCrouchWalkDecayRate;
        }
        else if (currentLocomotion == PlayerGroundedLocomotion.Sprinting)
        {
            targetSatietyDecay = player.satietySprintDecayRate;
        }

        player.Condition.satiety.decreaseRate = targetSatietyDecay;
    }
}
