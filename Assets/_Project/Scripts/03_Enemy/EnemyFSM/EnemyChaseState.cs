using UnityEngine;

public class EnemyChaseState : EnemyState
{
    private Vector3 lastNoisePosition;
    
    public EnemyChaseState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.speed = enemy.Data.chaseSpeed;
        enemy.NotifyAnimatorState(1);

        lastNoisePosition = enemy.DetectedNoisePosition;
        if (enemy.TrySetDestination(lastNoisePosition, out Vector3 destination))
        {
            enemy.SetCurrentInvestigationPosition(destination);
        }
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", enemy.Agent.velocity.magnitude / enemy.Data.chaseSpeed, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

        if (enemy.IsPlayerInAttackRadius())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

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
}
