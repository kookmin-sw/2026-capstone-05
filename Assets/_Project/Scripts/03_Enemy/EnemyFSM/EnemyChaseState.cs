using UnityEngine;

public class EnemyChaseState : EnemyState
{
    public EnemyChaseState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.speed = enemy.Data.chaseSpeed;
        enemy.Agent.SetDestination(enemy.DetectedNoisePosition);
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        // 소음 감지 시 해당 위치로 이동
        if (enemy.HasNoiseDetected)
        {
            enemy.ConsumeNoiseDetection();
            enemy.Agent.SetDestination(enemy.DetectedNoisePosition);
            return;
        }

        // 목적지 도착 시 SearchState 진입
        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            stateMachine.ChangeState(enemy.SearchState);
        }
    }
}
