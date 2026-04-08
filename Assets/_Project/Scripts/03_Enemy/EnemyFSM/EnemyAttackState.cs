using UnityEngine;

public class EnemyAttackState : EnemyState
{
    private EnemyAnimationEventHandler animationEventHandler;
    private bool isCoolingDown;
    private float cooldownTimer;


    public EnemyAttackState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        animationEventHandler = enemy.AnimationEventHandler;
        animationEventHandler.OnAttackStart += HandleAttackStart;
        animationEventHandler.OnAttackEnd += HandleAttackEnd;
        animationEventHandler.OnAttackFinish += HandleAttackFinish;

        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        if (!isCoolingDown)
        {
            enemy.Animator.SetInteger("WaitIndex", Random.Range(0, 2));
            enemy.Animator.SetTrigger("Attack");
        }
    }

    public override void Exit()
    {
        animationEventHandler.OnAttackStart -= HandleAttackStart;
        animationEventHandler.OnAttackEnd -= HandleAttackEnd;
        animationEventHandler.OnAttackFinish -= HandleAttackFinish;

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

        enemy.LookDetectedNoisePosition();

        if (isCoolingDown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isCoolingDown = false;
                if (enemy.IsPlayerInAttackRadius())
                {
                    enemy.Animator.SetInteger("WaitIndex", Random.Range(0, 2));
                    enemy.Animator.SetTrigger("Attack");
                }
                else
                    stateMachine.ChangeState(enemy.SearchState);
            }
        }
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

    private void HandleAttackFinish()
    {
        isCoolingDown = true;
        cooldownTimer = enemy.Data.attackCooldown;
        string attackIdleState = Random.value > 0.5f ? "Wait1" : "Wait2";
        enemy.Animator.CrossFade(attackIdleState, 0.2f);
    }
}
