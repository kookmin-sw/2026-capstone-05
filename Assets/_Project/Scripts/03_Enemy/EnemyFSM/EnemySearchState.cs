using UnityEngine;
using UnityEngine.AI;

public class EnemySearchState : EnemyState
{
    private enum EnemySearchPhase { Approaching, Searching }

    private EnemySearchPhase phase;
    private Vector3 searchCenter;
    private Vector3 lastNoisePosition;
    private float waitTimer;
    private bool isWaiting;

    public EnemySearchState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        searchCenter = enemy.DetectedNoisePosition;
        lastNoisePosition = searchCenter;
        enemy.SetPatrolCenter(searchCenter);
        enemy.Agent.speed = enemy.Data.searchSpeed;
        enemy.Agent.isStopped = false;
        phase = EnemySearchPhase.Approaching;
        isWaiting = false;
        SetApproachDestination();
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
    }

    public override void LogicUpdate()
    {
        if (enemy.IsPlayerInAttackRadius())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

        if (enemy.DetectedNoisePosition != lastNoisePosition)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.SuspicionLevel < enemy.Data.alertThreshold)
        {
            stateMachine.ChangeState(enemy.PatrolState);
            return;
        }

        if (phase == EnemySearchPhase.Approaching)
            UpdateApproachPhase();
        else
            UpdateSearchPhase();
    }

    private void UpdateApproachPhase()
    {
        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            phase = EnemySearchPhase.Searching;
            SetSearchDestination();
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
                SetSearchDestination();
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

    private void SetApproachDestination()
    {
        Vector3 targetDirection = searchCenter - enemy.transform.position;
        targetDirection.y = 0f;

        float approachOffset = Random.Range(1.5f, 3f);
        float approachDist = Mathf.Max(0f, targetDirection.magnitude - approachOffset);
        
        Vector3 approachPos = enemy.transform.position + targetDirection.normalized * approachDist;

        const float navMeshSampleRange = 2f;
        if (NavMesh.SamplePosition(approachPos, out NavMeshHit hit, navMeshSampleRange, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(hit.position);
        }
        else
        {
            phase = EnemySearchPhase.Searching;
            SetSearchDestination();
        }
    }

    private void SetSearchDestination()
    {
        const int maxSamplingAttempts = 5;
        const float navMeshSampleRange = 2f;

        for (int i = 0; i < maxSamplingAttempts; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * enemy.Data.searchRadius;
            Vector3 targetSearchPos = searchCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(targetSearchPos, out NavMeshHit hit, navMeshSampleRange, NavMesh.AllAreas))
            {
                enemy.Agent.SetDestination(hit.position);
                return;
            }
        }

        if (NavMesh.SamplePosition(searchCenter, out NavMeshHit fallback, navMeshSampleRange, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(fallback.position);
        }
    }
}
