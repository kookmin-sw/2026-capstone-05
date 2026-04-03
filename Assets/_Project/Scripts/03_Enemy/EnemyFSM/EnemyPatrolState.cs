using UnityEngine;
using UnityEngine.AI;

public class EnemyPatrolState : EnemyState
{
    private int sectorIndex;
    private float stuckTimer;
    private Vector3 lastCheckedPosition;

    public EnemyPatrolState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.speed = enemy.Data.walkSpeed;
        
        stuckTimer = 0f;
        lastCheckedPosition = enemy.transform.position;
        SetPatrolDestination();
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", enemy.Agent.velocity.magnitude / enemy.Data.chaseSpeed, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

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

        if (enemy.SuspicionLevel >= enemy.Data.alertThreshold)
        {
            stateMachine.ChangeState(enemy.AlertState);
            return;
        }

        if (CheckStuck())
        {
            SetPatrolDestination();
            return;
        }

        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            stateMachine.ChangeState(enemy.IdleState);
        }
    }

    private bool CheckStuck()
    {
        stuckTimer += Time.deltaTime;
        if (stuckTimer < 3f) return false;

        stuckTimer = 0f;
        float moved = Vector3.Distance(enemy.transform.position, lastCheckedPosition);
        lastCheckedPosition = enemy.transform.position;
        return moved < 0.2f;
    }

    private void SetPatrolDestination()
    {
        float sectorAngle = 60f * sectorIndex; 
        float randomAngle = sectorAngle + Random.Range(-25f, 25f);
        float randomDistance = enemy.Data.patrolRadius * Random.Range(0.8f, 1.0f);

        float angleInRad = randomAngle * Mathf.Deg2Rad;
        Vector3 targetOffset = new Vector3(
            Mathf.Sin(angleInRad) * randomDistance, 
            0f, 
            Mathf.Cos(angleInRad) * randomDistance
        );

        Vector3 targetWorldPos = enemy.PatrolCenter + targetOffset;

        sectorIndex = (sectorIndex + 1) % 6;

        const float navMeshSampleRange = 2f;
        if (NavMesh.SamplePosition(targetWorldPos, out NavMeshHit hit, navMeshSampleRange, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(hit.position);
        }
        else
        {
            if (NavMesh.SamplePosition(enemy.PatrolCenter, out NavMeshHit fallback, navMeshSampleRange, NavMesh.AllAreas))
            {
                enemy.Agent.SetDestination(fallback.position);
            }
        }
    }
}