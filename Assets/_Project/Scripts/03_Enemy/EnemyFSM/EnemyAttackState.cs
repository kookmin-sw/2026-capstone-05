using UnityEngine;

public class EnemyAttackState : EnemyState
{
    private struct IgnoredCollisionPair
    {
        public Collider enemyCollider;
        public Collider playerCollider;
    }

    private EnemyAnimationEventHandler animationEventHandler;
    private readonly System.Collections.Generic.List<IgnoredCollisionPair> ignoredPlayerCollisionPairs = new();
    private bool isRecovering;
    private float recoveryTimer;
    private float attackFailSafeTimer;
    private Quaternion lockedAttackRotation;
    private bool hasLockedAttackRotation;
    private bool isJumpAttackMoving;
    private bool originalAgentEnabled;
    private bool originalAgentUpdatePosition;
    private bool originalAgentUpdateRotation;
    private int currentAttackIndex;
    private bool hasPendingJumpAttackMovement;
    private bool hasPlayedJumpLandingSound;
    private Vector3 jumpStartPosition;
    private Vector3 jumpLandingPosition;
    private float jumpMoveTimer;
    private float jumpMoveDuration;
    private bool bodyCollidersAreTriggers;

    public bool BlocksHitReaction { get; private set; }

    public EnemyAttackState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        animationEventHandler = enemy.AnimationEventHandler;
        animationEventHandler.OnAttackStart += HandleAttackStart;
        animationEventHandler.OnAttackEnd += HandleAttackEnd;
        animationEventHandler.OnAttackFinish += HandleAttackFinish;
        animationEventHandler.OnLanding += HandleLanding;

        originalAgentEnabled = enemy.Agent.enabled;
        originalAgentUpdatePosition = enemy.Agent.updatePosition;
        originalAgentUpdateRotation = enemy.Agent.updateRotation;
        currentAttackIndex = -1;
        hasPendingJumpAttackMovement = false;
        isJumpAttackMoving = false;
        SetBodyCollidersTrigger(false);
        hasPlayedJumpLandingSound = false;

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
        animationEventHandler.OnLanding -= HandleLanding;

        foreach (var col in enemy.AttackColliders)
            col.DisableAttackCollider();

        BlocksHitReaction = false;
        EndJumpAttackMovement(true);
        hasPendingJumpAttackMovement = false;
        isRecovering = false;

        if (!enemy.Agent.enabled)
        {
            enemy.Agent.enabled = true;
        }

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

        if (isJumpAttackMoving)
        {
            UpdateJumpAttackMovement();
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

        if (currentAttackIndex == 2 && hasPendingJumpAttackMovement && !isJumpAttackMoving)
        {
            BeginJumpAttackMovement(jumpLandingPosition);
        }
    }

    private void StartAttackAnimation()
    {
        enemy.RequestStateSound(EnemySoundCue.Attack);

        int attackIndex = SelectAttackIndex(out Quaternion attackRotation);
        lockedAttackRotation = attackRotation;
        enemy.transform.rotation = lockedAttackRotation;

        hasLockedAttackRotation = true;
        enemy.RegisterAttackStarted();
        BlocksHitReaction = true;

        enemy.Animator.SetInteger("AttackIndex", attackIndex);
        enemy.Animator.SetInteger("WaitIndex", Random.Range(0, 2));
        enemy.Animator.SetTrigger("Attack");
        enemy.NotifyAnimatorTrigger("Attack");
        attackFailSafeTimer = enemy.Data.attackAnimationFailSafeTime;
    }

    private void HandleAttackEnd(int index)
    {
        foreach (var col in enemy.AttackColliders)
            if (col.ColliderIndex == index) col.DisableAttackCollider();

        if (currentAttackIndex == 2)
        {
            EndJumpAttackMovement(true);
        }
    }

    private void HandleAttackFinish()
    {
        FinishAttackExecution();
    }

    private void HandleLanding()
    {
        if (currentAttackIndex != 2 || hasPlayedJumpLandingSound)
            return;

        hasPlayedJumpLandingSound = true;
        enemy.RequestStateSound(EnemySoundCue.Landing);
    }

    private void FinishAttackExecution()
    {
        foreach (var col in enemy.AttackColliders)
            col.DisableAttackCollider();

        EndJumpAttackMovement(true);
        hasPendingJumpAttackMovement = false;
        BlocksHitReaction = false;
        BeginRecovery();
    }

    private int SelectAttackIndex(out Quaternion attackRotation)
    {
        if (enemy.TryConsumePreparedJumpAttack(out attackRotation, out Vector3 landingPosition))
        {
            currentAttackIndex = 2;
            hasPendingJumpAttackMovement = true;
            hasPlayedJumpLandingSound = false;
            jumpLandingPosition = landingPosition;
            return 2;
        }

        currentAttackIndex = Random.value < enemy.Data.attackVariant1Chance ? 1 : 0;
        hasPendingJumpAttackMovement = false;
        isJumpAttackMoving = false;
        hasPlayedJumpLandingSound = false;
        if (!enemy.TryGetAttackTargetRotation(out attackRotation))
        {
            attackRotation = enemy.transform.rotation;
        }

        return currentAttackIndex;
    }

    private void BeginJumpAttackMovement(Vector3 landingPosition)
    {
        hasPendingJumpAttackMovement = false;
        isJumpAttackMoving = true;
        SetBodyCollidersTrigger(true);
        SetPlayerCollisionIgnored(true);
        jumpStartPosition = enemy.transform.position;

        Vector3 direction = landingPosition - enemy.transform.position;
        float originalDistance = direction.magnitude;
        float extendedDistance = originalDistance * enemy.Data.jumpAttackDistanceMultiplier;
        jumpLandingPosition = enemy.transform.position + direction.normalized * extendedDistance;
        jumpMoveTimer = 0f;
        jumpMoveDuration = Mathf.Max(0.05f, enemy.Data.jumpAttackMoveDuration);

        if (enemy.Agent.enabled)
        {
            enemy.Agent.ResetPath();
            enemy.Agent.isStopped = true;
            enemy.Agent.updatePosition = false;
            enemy.Agent.updateRotation = false;
            enemy.Agent.enabled = false;
        }
    }

    private void UpdateJumpAttackMovement()
    {
        jumpMoveTimer += Time.deltaTime;
        float progress = Mathf.Clamp(jumpMoveTimer / jumpMoveDuration, 0f, 0.98f);
        Vector3 position = Vector3.Lerp(jumpStartPosition, jumpLandingPosition, progress);
        position.y += Mathf.Sin(progress * Mathf.PI) * Mathf.Max(0f, enemy.Data.jumpAttackArcHeight);
        enemy.transform.position = position;
    }

    private void EndJumpAttackMovement(bool snapToLanding)
    {
        if (!isJumpAttackMoving)
            return;

        isJumpAttackMoving = false;
        SetPlayerCollisionIgnored(false);
        SetBodyCollidersTrigger(false);
        if (snapToLanding)
        {
            enemy.transform.position = jumpLandingPosition;
        }

        if (!enemy.Agent.enabled && originalAgentEnabled)
        {
            enemy.Agent.enabled = true;
        }

        enemy.TrySnapAgentToNearestNavMesh(enemy.Data.jumpAttackLandingSampleRange);
        if (enemy.Agent.enabled)
        {
            enemy.Agent.updatePosition = originalAgentUpdatePosition;
            enemy.Agent.updateRotation = originalAgentUpdateRotation;
        }
    }

    private void SetBodyCollidersTrigger(bool value)
    {
        if (bodyCollidersAreTriggers == value)
            return;

        foreach (var col in enemy.AttackColliders)
        {
            if (col == null)
                continue;

            col.SetBodyColliderTrigger(value);
        }

        bodyCollidersAreTriggers = value;
    }

    private void SetPlayerCollisionIgnored(bool value)
    {
        if (!value)
        {
            RestoreIgnoredPlayerCollisions();
            return;
        }

        RestoreIgnoredPlayerCollisions();

        Collider[] enemyColliders = enemy.GetComponentsInChildren<Collider>();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (Collider enemyCollider in enemyColliders)
        {
            if (!ShouldIgnoreEnemyCollider(enemyCollider))
                continue;

            foreach (GameObject player in players)
            {
                if (player == null)
                    continue;

                Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
                foreach (Collider playerCollider in playerColliders)
                {
                    if (playerCollider == null || !playerCollider.enabled)
                        continue;

                    Physics.IgnoreCollision(enemyCollider, playerCollider, true);
                    ignoredPlayerCollisionPairs.Add(new IgnoredCollisionPair
                    {
                        enemyCollider = enemyCollider,
                        playerCollider = playerCollider
                    });
                }
            }
        }
    }

    private bool ShouldIgnoreEnemyCollider(Collider enemyCollider)
    {
        if (enemyCollider == null || !enemyCollider.enabled)
            return false;

        EnemyAttackCollider attackCollider = enemyCollider.GetComponent<EnemyAttackCollider>();
        if (attackCollider == null)
            return !enemyCollider.isTrigger;

        return attackCollider.IsBodyCollider;
    }

    private void RestoreIgnoredPlayerCollisions()
    {
        for (int i = 0; i < ignoredPlayerCollisionPairs.Count; i++)
        {
            IgnoredCollisionPair pair = ignoredPlayerCollisionPairs[i];
            if (pair.enemyCollider != null && pair.playerCollider != null)
            {
                Physics.IgnoreCollision(pair.enemyCollider, pair.playerCollider, false);
            }
        }

        ignoredPlayerCollisionPairs.Clear();
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
