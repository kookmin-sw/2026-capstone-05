using UnityEngine;

public class PlayerInteractionState : PlayerState
{
    public PlayerInteractionState(PlayerController player, PlayerStateMachine stateMachine)
        : base(player, stateMachine)
    {

    }

    public override void Enter()
    {
        base.Enter();
        player.useGravity = true;

        player.Condition.stamina.increaseRate = player.idleRegenRate;
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
    }
}
