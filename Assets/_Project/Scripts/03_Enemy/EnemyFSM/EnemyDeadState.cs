using UnityEngine;

public class EnemyDeadState : EnemyState
{
    private EnemyAnimationEventHandler animationEventHandler;
    private float failSafeTimer;
    private bool hasFinished;

    public EnemyDeadState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.RequestStateSound(EnemySoundCue.Dead);

        animationEventHandler = enemy.AnimationEventHandler;
        animationEventHandler.OnDeadEnd += HandleDeadEnd;
        failSafeTimer = enemy.Data.deadAnimationFailSafeTime;
        hasFinished = false;

        enemy.Agent.isStopped = true;
        enemy.Agent.enabled = false;

        enemy.Animator.SetTrigger("Dead");
        enemy.NotifyAnimatorTrigger("Dead");
    }

    public override void Exit() { }

    public override void LogicUpdate()
    {
        failSafeTimer -= Time.deltaTime;
        if (failSafeTimer <= 0f)
        {
            HandleDeadEnd();
        }
    }

    private void HandleDeadEnd()
    {
        if (hasFinished)
            return;

        hasFinished = true;
        animationEventHandler.OnDeadEnd -= HandleDeadEnd;
        Object.Destroy(enemy.gameObject);
    }
}
