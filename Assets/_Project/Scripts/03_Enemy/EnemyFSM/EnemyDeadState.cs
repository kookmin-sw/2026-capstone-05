using UnityEngine;

public class EnemyDeadState : EnemyState
{
    private EnemyAnimationEventHandler animationEventHandler;

    public EnemyDeadState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        animationEventHandler = enemy.AnimationEventHandler;
        animationEventHandler.OnDeadEnd += HandleDeadEnd;

        enemy.Agent.isStopped = true;
        enemy.Agent.enabled = false;

        enemy.Animator.SetTrigger("Dead");
    }

    public override void Exit() { }

    public override void LogicUpdate() { }

    private void HandleDeadEnd()
    {
        animationEventHandler.OnDeadEnd -= HandleDeadEnd;
        Object.Destroy(enemy.gameObject);
    }
}
