using UnityEngine;

public class EnemyAttackState : EnemyState
{
    private EnemyAnimationEventHandler animationEventHandler;
    private bool isRecovering;
    private float recoveryTimer;
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

        if (!isRecovering)
        {
            if (enemy.CanStartAttack())
            {
                StartAttackAnimation();
            }
            else
            {
                ChangeToResponsiveState();
            }
        }
    }

    public override void Exit()
    {
        animationEventHandler.OnAttackStart -= HandleAttackStart;
        animationEventHandler.OnAttackEnd -= HandleAttackEnd;
        animationEventHandler.OnAttackFinish -= HandleAttackFinish;

        foreach (var col in enemy.AttackColliders)
            col.DisableAttackCollider();

        isRecovering = false;

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

        if (isRecovering)
        {
            UpdateRecovery();
        }
        else
        {
            attackFailSafeTimer -= Time.deltaTime;
            if (attackFailSafeTimer <= 0f)
            {
                FinishAttackExecution();
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
        if (enemy.TryGetAttackTargetRotation(out Quaternion attackRotation))
        {
            lockedAttackRotation = attackRotation;
            enemy.transform.rotation = lockedAttackRotation;
        }
        else
        {
            lockedAttackRotation = enemy.transform.rotation;
        }

        hasLockedAttackRotation = true;
        enemy.RegisterAttackStarted();

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
        FinishAttackExecution();
    }

    private void FinishAttackExecution()
    {
        foreach (var col in enemy.AttackColliders)
            col.DisableAttackCollider();

        BeginRecovery();
    }

    private void BeginRecovery()
    {
        if (isRecovering)
            return;

        isRecovering = true;
        float minTime = Mathf.Max(0.1f, enemy.Data.attackRecoveryMinTime);
        float maxTime = Mathf.Max(minTime, enemy.Data.attackRecoveryMaxTime);
        recoveryTimer = Random.Range(minTime, maxTime);

        bool useWait1 = Random.value > 0.5f;
        enemy.Animator.CrossFade(useWait1 ? "Wait1" : "Wait2", 0.2f);
        enemy.NotifyAnimatorState(useWait1 ? (byte)2 : (byte)3);
    }

    private void UpdateRecovery()
    {
        recoveryTimer -= Time.deltaTime;
        if (recoveryTimer > 0f)
            return;

        isRecovering = false;
        if (enemy.CanStartAttack())
        {
            StartAttackAnimation();
            return;
        }

        ChangeToResponsiveState();
    }

    private void ChangeToResponsiveState()
    {
        isRecovering = false;

        if (enemy.TryChangeStateBySuspicion())
            return;

        if (enemy.HasDetectedNoise)
        {
            stateMachine.ChangeState(enemy.SearchState);
            return;
        }

        stateMachine.ChangeState(enemy.AlertState);
    }
}
