using UnityEngine;
using UnityEngine.AI;

public class EnemyPatrolState : EnemyState
{
    private System.Random patrolRandom;
    private bool hasPatrolPattern;
    private float currentAngle;
    private int patrolDirection;
    private float stuckTimer;
    private Vector3 lastCheckedPosition;

    public EnemyPatrolState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        EnsurePatrolPattern();
        enemy.Agent.speed = enemy.Data.walkSpeed;
        
        stuckTimer = 0f;
        lastCheckedPosition = enemy.transform.position;
        SetPatrolDestination();
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", enemy.Agent.velocity.magnitude / enemy.Data.walkSpeed, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

        if (enemy.TryChangeStateBySuspicion())
            return;

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
        int attempts = Mathf.Max(1, enemy.Data.patrolDestinationAttempts);

        for (int i = 0; i < attempts; i++)
        {
            Vector3 targetWorldPos = GetNextPatrolCandidate();
            if (enemy.TrySetDestination(targetWorldPos))
            {
                return;
            }
        }

        enemy.TrySetDestination(enemy.PatrolCenter);
    }

    private void EnsurePatrolPattern()
    {
        if (hasPatrolPattern)
            return;

        unchecked
        {
            Vector3 position = enemy.transform.position;
            int seed = enemy.GetInstanceID();
            seed = seed * 397 ^ Mathf.RoundToInt(position.x * 100f);
            seed = seed * 397 ^ Mathf.RoundToInt(position.y * 100f);
            seed = seed * 397 ^ Mathf.RoundToInt(position.z * 100f);
            patrolRandom = new System.Random(seed);
        }

        currentAngle = NextFloat(0f, 360f);
        patrolDirection = patrolRandom.NextDouble() < 0.5 ? -1 : 1;
        hasPatrolPattern = true;
    }

    private Vector3 GetNextPatrolCandidate()
    {
        if (patrolRandom.NextDouble() < enemy.Data.patrolDirectionChangeChance)
        {
            patrolDirection *= -1;
        }

        float minStep = Mathf.Min(enemy.Data.patrolMinAngleStep, enemy.Data.patrolMaxAngleStep);
        float maxStep = Mathf.Max(enemy.Data.patrolMinAngleStep, enemy.Data.patrolMaxAngleStep);
        currentAngle = Mathf.Repeat(currentAngle + NextFloat(minStep, maxStep) * patrolDirection, 360f);

        float minRatio = Mathf.Clamp01(Mathf.Min(enemy.Data.patrolMinRadiusRatio, enemy.Data.patrolMaxRadiusRatio));
        float maxRatio = Mathf.Clamp01(Mathf.Max(enemy.Data.patrolMinRadiusRatio, enemy.Data.patrolMaxRadiusRatio));
        float radiusRatio = Mathf.Lerp(minRatio, maxRatio, Mathf.Sqrt(NextFloat(0f, 1f)));
        float distance = enemy.Data.patrolRadius * radiusRatio;

        float angleInRad = currentAngle * Mathf.Deg2Rad;
        Vector3 targetOffset = new Vector3(
            Mathf.Sin(angleInRad) * distance,
            0f,
            Mathf.Cos(angleInRad) * distance
        );

        return enemy.PatrolCenter + targetOffset;
    }

    private float NextFloat(float min, float max)
    {
        return min + (float)patrolRandom.NextDouble() * (max - min);
    }
}
