using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float stopTimer;

    public EnemyIdleState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        stopTimer = Random.Range(enemy.Data.idleStartTime, enemy.Data.idleEndTime);
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        if (enemy.SuspicionLevel >= enemy.Data.chaseThreshold)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.SuspicionLevel >= enemy.Data.searchThreshold)
        {
            stateMachine.ChangeState(enemy.SearchState);
            return;
        }

        if (enemy.SuspicionLevel > enemy.Data.alertThreshold)
        {
            stateMachine.ChangeState(enemy.AlertState);
            return;
        }

        stopTimer -= Time.deltaTime;
        if (stopTimer <= 0f)
        {
            stateMachine.ChangeState(enemy.PatrolState);
        }
    }
}
