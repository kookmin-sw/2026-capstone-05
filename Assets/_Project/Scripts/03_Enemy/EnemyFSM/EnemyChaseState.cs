using UnityEngine;

public class EnemyChaseState : EnemyState
{
    private Vector3 lastNoisePosition;

    public EnemyChaseState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.speed = enemy.Data.chaseSpeed;
        lastNoisePosition = enemy.DetectedNoisePosition;
        enemy.Agent.SetDestination(lastNoisePosition);
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        if (enemy.DetectedNoisePosition != lastNoisePosition)
        {
            lastNoisePosition = enemy.DetectedNoisePosition;
            enemy.Agent.SetDestination(lastNoisePosition);
            enemy.LookAtDetectedNoisePosition();
        }

        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            enemy.SetSuspicionLevel(enemy.Data.suspicionOnChaseArrival);
            stateMachine.ChangeState(enemy.SearchState);
        }
    }
}
