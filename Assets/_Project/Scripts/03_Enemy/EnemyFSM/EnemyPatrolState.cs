using UnityEngine;
using UnityEngine.AI;

public class EnemyPatrolState : EnemyState
{
    private readonly Vector3 patrolOrigin;

    public EnemyPatrolState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine)
    {
        patrolOrigin = enemy.transform.position;
    }

    public override void Enter()
    {
        enemy.Agent.speed = enemy.Data.walkSpeed;
        SetRandomDestination();
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        // 소음 감지 시 AlertState 진입
        if (enemy.HasNoiseDetected)
        {
            enemy.ConsumeNoiseDetection();
            stateMachine.ChangeState(enemy.AlertState);
            return;
        }

        // 목적지 도착 시 IdleState 진입
        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            stateMachine.ChangeState(enemy.IdleState);
        }
    }

    private void SetRandomDestination()
    {
        const int maxAttempts = 5;
        const float navMeshSampleRadius = 2f;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * enemy.Data.patrolRadius;
            Vector3 candidate = patrolOrigin + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                enemy.Agent.SetDestination(hit.position);
                return;
            }
        }

        // 모든 시도 실패 시 원점으로 복귀
        if (NavMesh.SamplePosition(patrolOrigin, out NavMeshHit fallback, navMeshSampleRadius, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(fallback.position);
        }
    }
}
