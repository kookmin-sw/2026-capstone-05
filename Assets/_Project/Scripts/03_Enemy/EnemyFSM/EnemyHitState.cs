using UnityEngine;

public class EnemyHitState : EnemyState
{
    private EnemyAnimationEventHandler animationEventHandler;


    public EnemyHitState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        animationEventHandler = enemy.AnimationEventHandler;
        animationEventHandler.OnHitEnd += HandleHitEnd;

        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        enemy.Animator.SetTrigger("Hit");
    }

    public override void Exit()
    {
        animationEventHandler.OnHitEnd -= HandleHitEnd;

        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", 0f, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);
    }

    private void HandleHitEnd()
    {
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

        stateMachine.ChangeState(enemy.PatrolState);
    }
}