using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float idleTimer;
    private float elapsedTime;

    public EnemyIdleState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        idleTimer = Random.Range(enemy.Data.idleStartTime, enemy.Data.idleEndTime);
        elapsedTime = 0f;
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

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= idleTimer)
        {
            stateMachine.ChangeState(enemy.PatrolState);
        }
    }
}
