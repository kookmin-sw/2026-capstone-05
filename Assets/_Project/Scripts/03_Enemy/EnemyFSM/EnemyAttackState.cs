using UnityEngine;

public class EnemyAttackState : EnemyState
{
    private EnemyAnimationEventHandler animationEventHandler;


    public EnemyAttackState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        animationEventHandler = enemy.AnimationEventHandler;
        animationEventHandler.OnAttackStart += HandleAttackStart;
        animationEventHandler.OnAttackEnd += HandleAttackEnd;
        animationEventHandler.OnFinalAttackEnd += HandleFinalAttackEnd;

        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        enemy.Animator.SetTrigger("Attack");
    }

    public override void Exit()
    {
        animationEventHandler.OnAttackStart -= HandleAttackStart;
        animationEventHandler.OnAttackEnd -= HandleAttackEnd;
        animationEventHandler.OnFinalAttackEnd -= HandleFinalAttackEnd;

        foreach (var col in enemy.AttackColliders)
            col.DisableAttackCollider();

        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;

        enemy.Animator.CrossFade("Locomotion", 0.2f);
    }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", 0f, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

        enemy.LookAtDetectedNoisePosition();
    }

    private void HandleAttackStart(int index)
    {
        foreach (var col in enemy.AttackColliders)
            if (col.ColliderIndex == index) col.EnableAttackCollider();
    }

    private void HandleAttackEnd(int index)
    {
        foreach (var col in enemy.AttackColliders)
            if (col.ColliderIndex == index) col.DisableAttackCollider();
    }

    private void HandleFinalAttackEnd()
    {
        if (enemy.IsPlayerInAttackRadius())
        {
            enemy.Animator.SetTrigger("Attack");
            return;
        }

        stateMachine.ChangeState(enemy.ChaseState);
    }
}
