using UnityEngine;

public class EnemyHitState : EnemyState
{
    public EnemyHitState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter() { }

    public override void Exit() { }

    public override void LogicUpdate() { }
}