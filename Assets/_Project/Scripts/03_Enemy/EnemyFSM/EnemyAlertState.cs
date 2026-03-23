using UnityEngine;

public class EnemyAlertState : EnemyState
{
    public EnemyAlertState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        if (enemy.SuspicionLevel >= 100f)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.SuspicionLevel < 50f)
        {
            stateMachine.ChangeState(enemy.PatrolState);
            return;
        }

        enemy.LookAtDetectedNoisePosition();
    }
}
