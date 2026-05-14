using UnityEngine;

public class EnemyHitState : EnemyState
{
    private EnemyAnimationEventHandler animationEventHandler;
    private float failSafeTimer;
    private bool hasFinished;


    public EnemyHitState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        animationEventHandler = enemy.AnimationEventHandler;
        animationEventHandler.OnHitEnd += HandleHitEnd;
        hasFinished = false;

        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        PlayHitAnimation();
    }

    public override void Exit()
    {
        animationEventHandler.OnHitEnd -= HandleHitEnd;

        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;

        enemy.Animator.CrossFade("Locomotion", 0.2f);
    }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", 0f, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

        failSafeTimer -= Time.deltaTime;
        if (failSafeTimer <= 0f)
        {
            HandleHitEnd();
        }
    }

    private void HandleHitEnd()
    {
        if (hasFinished)
            return;

        hasFinished = true;

        if (enemy.CanStartAttack())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

        if (enemy.TryChangeStateBySuspicion())
            return;

        EnemyState interruptedState = enemy.ConsumeInterruptedStateBeforeHit();
        if (interruptedState != null &&
            interruptedState != enemy.HitState &&
            interruptedState != enemy.DeadState &&
            interruptedState != enemy.AttackState)
        {
            stateMachine.ChangeState(interruptedState);
            return;
        }

        stateMachine.ChangeState(enemy.PatrolState);
    }

    public void RestartReaction()
    {
        hasFinished = false;
        PlayHitAnimation();
    }

    private void PlayHitAnimation()
    {
        enemy.RequestStateSound(EnemySoundCue.Hit);

        enemy.Animator.SetInteger("HitIndex", Random.Range(0, 2));
        enemy.Animator.ResetTrigger("Hit");
        enemy.Animator.SetTrigger("Hit");
        enemy.NotifyAnimatorTrigger("Hit");
        failSafeTimer = enemy.Data.hitAnimationFailSafeTime;
    }
}
