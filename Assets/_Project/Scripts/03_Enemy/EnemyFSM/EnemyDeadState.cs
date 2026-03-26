using UnityEngine;

public class EnemyDeadState : EnemyState
{
    public EnemyDeadState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter() { }

    public override void Exit() { }

    public override void LogicUpdate() { }
}
