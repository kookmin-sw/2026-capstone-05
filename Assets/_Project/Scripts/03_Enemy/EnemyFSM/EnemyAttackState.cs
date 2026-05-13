using UnityEngine;

public class EnemyAttackState : EnemyState
{
    private EnemyAnimationEventHandler animationEventHandler;
    private bool isCoolingDown;
    private float cooldownTimer;
    private float attackFailSafeTimer;
    private Quaternion lockedAttackRotation;
    private bool hasLockedAttackRotation;


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
        lockedAttackRotation = enemy.transform.rotation;
        hasLockedAttackRotation = true;

        if (!isCoolingDown)
        {
            StartAttackAnimation();
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
        enemy.NotifyAnimatorState(1);
    }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", 0f, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

        if (hasLockedAttackRotation)
        {
            enemy.transform.rotation = lockedAttackRotation;
        }

        if (isCoolingDown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isCoolingDown = false;
                if (enemy.IsPlayerInAttackRadius())
                {
                    StartAttackAnimation();
                }
                else
                    stateMachine.ChangeState(enemy.SearchState);
            }
        }
        else
        {
            attackFailSafeTimer -= Time.deltaTime;
            if (attackFailSafeTimer <= 0f)
            {
                HandleAttackFinish();
            }
        }
    }

    private void HandleAttackStart(int index)
    {
        foreach (var col in enemy.AttackColliders)
            if (col.ColliderIndex == index) col.EnableAttackCollider();
    }

    private void StartAttackAnimation()
    {
        lockedAttackRotation = enemy.transform.rotation;
        hasLockedAttackRotation = true;

        enemy.Animator.SetInteger("WaitIndex", Random.Range(0, 2));
        enemy.Animator.SetTrigger("Attack");
        enemy.NotifyAnimatorTrigger("Attack");
        attackFailSafeTimer = enemy.Data.attackAnimationFailSafeTime;
    }

    private void HandleAttackEnd(int index)
    {
        foreach (var col in enemy.AttackColliders)
            if (col.ColliderIndex == index) col.DisableAttackCollider();
    }

    private void HandleAttackFinish()
    {
        if (isCoolingDown)
            return;

        isCoolingDown = true;
        cooldownTimer = enemy.Data.attackCooldown;
        bool useWait1 = Random.value > 0.5f;
        enemy.Animator.CrossFade(useWait1 ? "Wait1" : "Wait2", 0.2f);
        enemy.NotifyAnimatorState(useWait1 ? (byte)2 : (byte)3);
    }
}
