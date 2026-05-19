using UnityEngine;

public class EnemyChaseState : EnemyState
{
    private Vector3 lastNoisePosition;
    private float nextChaseSoundTime;
    
    public EnemyChaseState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.speed = enemy.Data.chaseSpeed;
        enemy.NotifyAnimatorState(1);
        ScheduleNextChaseSound(enemy.Data.chaseSoundInitialDelayMin, enemy.Data.chaseSoundInitialDelayMax);

        lastNoisePosition = enemy.DetectedNoisePosition;
        if (enemy.TrySetDestination(lastNoisePosition, out Vector3 destination))
        {
            enemy.SetCurrentInvestigationPosition(destination);
        }
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        enemy.SetRunLocomotionSpeed(enemy.Agent.velocity.magnitude, enemy.Data.chaseSpeed);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

        if (enemy.CanStartAttack())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

        TryPlayMovingChaseSound();

        if (enemy.HasMeaningfullyNewNoise(lastNoisePosition))
        {
            lastNoisePosition = enemy.DetectedNoisePosition;
            if (enemy.TrySetDestination(lastNoisePosition, out Vector3 destination))
            {
                enemy.SetCurrentInvestigationPosition(destination);
            }
        }

        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            stateMachine.ChangeState(enemy.SearchState);
            return;
        }

        if (enemy.Suspicion < enemy.Data.chaseExitThreshold)
        {
            stateMachine.ChangeState(enemy.SearchState);
        }
    }

    private void TryPlayMovingChaseSound()
    {
        if (Time.time < nextChaseSoundTime)
            return;

        float velocityThreshold = enemy.Data.movingSoundVelocityThreshold;
        if (enemy.Agent.pathPending ||
            enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance ||
            enemy.Agent.velocity.sqrMagnitude < velocityThreshold * velocityThreshold)
        {
            return;
        }

        enemy.RequestStateSound(EnemySoundCue.Chase);
        ScheduleNextChaseSound(enemy.Data.chaseSoundIntervalMin, enemy.Data.chaseSoundIntervalMax);
    }

    private void ScheduleNextChaseSound(float minInterval, float maxInterval)
    {
        float min = Mathf.Min(minInterval, maxInterval);
        float max = Mathf.Max(minInterval, maxInterval);
        nextChaseSoundTime = Time.time + Random.Range(min, max);
    }
}
