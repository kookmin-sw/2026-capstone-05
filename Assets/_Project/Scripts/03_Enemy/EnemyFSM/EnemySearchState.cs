using UnityEngine;
using UnityEngine.AI;

public class EnemySearchState : EnemyState
{
    private Vector3 searchCenter;

    public EnemySearchState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        searchCenter = enemy.transform.position;
        enemy.SetPatrolOrigin(searchCenter);
        enemy.Agent.speed = enemy.Data.searchSpeed;
        SetRandomDestination();
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        // 공격 범위 내 플레이어 존재 시 AttackState 진입
        if (IsPlayerInAttackRange())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

        // 수치 100 이상: 소음 재감지 → ChaseState
        if (enemy.NoiseSuspicionLevel >= 100f)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        // 수치 0: 의심 해소 → IdleState
        if (enemy.NoiseSuspicionLevel <= 0f)
        {
            stateMachine.ChangeState(enemy.IdleState);
            return;
        }

        // 목적지 도착 시 다음 랜덤 위치로 수색
        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            SetRandomDestination();
        }
    }

    private bool IsPlayerInAttackRange()
    {
        Collider[] hits = Physics.OverlapSphere(enemy.transform.position, enemy.Data.attackRange);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
                return true;
        }
        return false;
    }

    private void SetRandomDestination()
    {
        const int maxAttempts = 5;
        const float navMeshSampleRadius = 2f;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * enemy.Data.searchRadius;
            Vector3 candidate = searchCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                enemy.Agent.SetDestination(hit.position);
                return;
            }
        }

        // 모든 시도 실패 시 수색 중심으로 복귀
        if (NavMesh.SamplePosition(searchCenter, out NavMeshHit fallback, navMeshSampleRadius, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(fallback.position);
        }
    }
}
