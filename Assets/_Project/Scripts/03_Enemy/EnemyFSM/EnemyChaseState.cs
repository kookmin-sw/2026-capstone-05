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
        // 새 소음 위치 감지 시 목적지 업데이트 (의심 수치는 감소하지 않음)
        if (enemy.DetectedNoisePosition != lastNoisePosition)
        {
            lastNoisePosition = enemy.DetectedNoisePosition;
            enemy.Agent.SetDestination(lastNoisePosition);
        }

        // 목적지 도착 시 SearchState 진입
        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            stateMachine.ChangeState(enemy.SearchState);
        }
    }
}
