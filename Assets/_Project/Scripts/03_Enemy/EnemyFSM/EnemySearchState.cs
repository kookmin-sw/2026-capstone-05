using UnityEngine;
using UnityEngine.AI;

public class EnemySearchState : EnemyState
{
    private enum Phase { Approaching, Searching }

    private Phase phase;
    private Vector3 midpoint;
    private Vector3 searchCenter;
    private float waitTimer;
    private bool isWaiting;

    public EnemySearchState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        midpoint = Vector3.Lerp(enemy.transform.position, enemy.DetectedNoisePosition, 0.5f);
        searchCenter = midpoint;
        enemy.SetPatrolCenter(searchCenter);
        enemy.Agent.speed = enemy.Data.searchSpeed;
        enemy.Agent.isStopped = false;
        phase = Phase.Approaching;
        isWaiting = false;
        waitTimer = 0f;
        SetApproachDestination();
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
    }

    public override void LogicUpdate()
    {
        if (CanAttack())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

        if (enemy.SuspicionLevel >= enemy.Data.chaseThreshold)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.SuspicionLevel < enemy.Data.alertThreshold)
        {
            stateMachine.ChangeState(enemy.PatrolState);
            return;
        }

        if (phase == Phase.Approaching)
            UpdateApproachPhase();
        else
            UpdateSearchPhase();
    }

    private void UpdateApproachPhase()
    {
        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            phase = Phase.Searching;
            SetRandomDestination();
        }
    }

    private void UpdateSearchPhase()
    {
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

    private bool CanAttack()
    {
        Collider[] hits = Physics.OverlapSphere(enemy.transform.position, enemy.Data.attackRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
                return true;
        }
        return false;
    }

    private void SetApproachDestination()
    {
        const float navMeshSampleRadius = 2f;

        if (NavMesh.SamplePosition(midpoint, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(hit.position);
        }
        else
        {
            phase = Phase.Searching;
            SetRandomDestination();
        }
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
