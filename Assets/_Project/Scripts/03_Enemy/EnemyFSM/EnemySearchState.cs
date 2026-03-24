using UnityEngine;
using UnityEngine.AI;

public class EnemySearchState : EnemyState
{
    private Vector3 searchCenter;
    private float previousSuspicionLevel;
    private float waitTimer;
    private bool isWaiting;

    public EnemySearchState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        previousSuspicionLevel = enemy.SuspicionLevel;
        isWaiting = false;
        waitTimer = 0f;
        searchCenter = enemy.transform.position;
        enemy.SetPatrolCenter(searchCenter);
        enemy.Agent.speed = enemy.Data.searchSpeed;
        SetRandomDestination();
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
    }

    public override void LogicUpdate()
    {
        if (IsPlayerInAttackRadius())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

        if (enemy.SuspicionLevel > previousSuspicionLevel)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.SuspicionLevel <= 0f)
        {
            stateMachine.ChangeState(enemy.PatrolState);
            return;
        }

        previousSuspicionLevel = enemy.SuspicionLevel;

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                enemy.Agent.isStopped = false;
                SetRandomDestination();
            }
            return;
        }

        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            isWaiting = true;
            waitTimer = Random.Range(enemy.Data.waitStartTime, enemy.Data.waitEndTime);
            enemy.Agent.isStopped = true;
        }
    }

    private bool IsPlayerInAttackRadius()
    {
        Collider[] hits = Physics.OverlapSphere(enemy.transform.position, enemy.Data.attackRadius);
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
        
        if (NavMesh.SamplePosition(searchCenter, out NavMeshHit fallback, navMeshSampleRadius, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(fallback.position);
        }
    }
}
