using UnityEngine;

public class PlayerAirborneState : PlayerState
{
    private float airControlMultiplier = 1f;
    private float airControlLerpSpeed = 10f;

    public PlayerAirborneState(PlayerController player, PlayerStateMachine stateMachine)
        : base(player, stateMachine)
    {

    }

    public override void Enter()
    {
        base.Enter();

        player.useGravity = true;
        player.canLook = true;

        player.Animator.SetGrounded(false);
    }

    public override void Exit()
    {
        base.Exit();

        player.Animator.SetGrounded(true);
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        if (player.IsGrounded && player.currentVelocity.y <= 0f)
        {
            stateMachine.ChangeState(player.GroundedState);
            return;
        }

        HandleAirMovement();
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
    }


    private void HandleAirMovement()
    {
        Vector2 input = player.InputHandler.MoveInput;
        Vector3 moveDirection = (player.transform.right * input.x + player.transform.forward * input.y).normalized;

        float targetSpeed = player.walkSpeed * airControlMultiplier;

        player.currentVelocity.x = Mathf.Lerp(player.currentVelocity.x, moveDirection.x * targetSpeed, Time.deltaTime * airControlLerpSpeed);
        player.currentVelocity.z = Mathf.Lerp(player.currentVelocity.z, moveDirection.z * targetSpeed, Time.deltaTime * airControlLerpSpeed);
    }
}
