using Fusion;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(NetworkObject))]
public class EnemyAI : NetworkBehaviour, INoiseListener
{
    private const int MaxNoiseMemorySlots = 8;

    private struct NoiseMemorySlot
    {
        public bool isActive;
        public Vector3 sourcePosition;
        public Vector3 estimatedPosition;
        public NoiseData.NoiseType noiseType;
        public float intensity;
        public float score;
        public float lastHeardTime;
        public bool isObstructed;
        public int repeatCount;
    }

    // Serialized Fields
    [SerializeField] private EnemyData data;

    // Private Fields
    private float lastNoiseTime;
    private float rotationVelocity;
    private readonly Collider[] attackCheckBuffer = new Collider[16];
    private int lastAttackTriggerCount;
    private int lastHitTriggerCount;
    private int lastDeadTriggerCount;
    private int lastTurnLeftTriggerCount;
    private int lastTurnRightTriggerCount;
    private bool isLocalSimulationActive;
    private bool isNetworkSpawned;
    private bool isRuntimeInitialized;
    private Vector3 localDetectedNoisePosition;
    private float localSuspicion;
    private bool localHasDetectedNoise;
    private int localLastNoiseTypeRaw;
    private float localLastNoiseIntensity;
    private float localLastNoiseTime;
    private Vector3 localLastConfirmedNoisePosition;
    private Vector3 localCurrentInvestigationPosition;
    private readonly NoiseMemorySlot[] noiseMemorySlots = new NoiseMemorySlot[MaxNoiseMemorySlots];
    private int activeNoiseMemoryIndex = -1;
    private float nextNoiseRetargetTime;
    private float noiseRetargetLockedUntil;
    private float nextAttackAllowedTime;
    private float nextJumpAttackDecisionTime;
    private bool hasPreparedJumpAttack;
    private Vector3 preparedJumpAttackLandingPosition;
    private Quaternion preparedJumpAttackRotation;

    [Networked] private Vector3 NetworkPosition { get; set; }
    [Networked] private Quaternion NetworkRotation { get; set; }
    [Networked] private Vector3 NetworkDetectedNoisePosition { get; set; }
    [Networked] private float NetworkSuspicion { get; set; }
    [Networked] private NetworkBool NetworkHasDetectedNoise { get; set; }
    [Networked] private int NetworkLastNoiseTypeRaw { get; set; }
    [Networked] private float NetworkLastNoiseIntensity { get; set; }
    [Networked] private float NetworkLastNoiseTime { get; set; }
    [Networked] private Vector3 NetworkLastConfirmedNoisePosition { get; set; }
    [Networked] private Vector3 NetworkCurrentInvestigationPosition { get; set; }

    [Networked] private float NetworkAnimSpeed { get; set; }
    [Networked] private float NetworkAnimAngle { get; set; }
    [Networked] private NetworkBool NetworkAnimIsAlert { get; set; }
    [Networked] private int NetworkAnimWaitIndex { get; set; }
    [Networked] private int NetworkAnimHitIndex { get; set; }
    [Networked] private int NetworkAnimAttackIndex { get; set; }
    [Networked] private int NetworkAttackTriggerCount { get; set; }
    [Networked] private int NetworkHitTriggerCount { get; set; }
    [Networked] private int NetworkDeadTriggerCount { get; set; }
    [Networked] private int NetworkTurnLeftTriggerCount { get; set; }
    [Networked] private int NetworkTurnRightTriggerCount { get; set; }
    [Networked] private int NetworkAnimStateChangeCount { get; set; }
    [Networked] private byte NetworkAnimStateId { get; set; }

    // Properties: Core
    public EnemyData Data => data;
    public EnemyStateMachine StateMachine { get; private set; }
    public Animator Animator { get; private set; }
    public NavMeshAgent Agent { get; private set; }
    public EnemyHealth Health { get; private set; }
    public EnemyAudioController AudioController { get; private set; }
    public EnemyAnimationEventHandler AnimationEventHandler { get; private set; }
    public EnemyAttackCollider[] AttackColliders { get; private set; }
    private int lastAnimStateChangeCount;

    // Properties: States
    public EnemyIdleState IdleState { get; private set; }
    public EnemyPatrolState PatrolState { get; private set; }
    public EnemyAlertState AlertState { get; private set; }
    public EnemyChaseState ChaseState { get; private set; }
    public EnemySearchState SearchState { get; private set; }
    public EnemyAttackState AttackState { get; private set; }
    public EnemyHitState HitState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }
    public EnemyState InterruptedStateBeforeHit { get; private set; }

    // Properties: Gameplay
    public Vector3 PatrolCenter { get; private set; }
    public Vector3 DetectedNoisePosition => UsesLocalState ? localDetectedNoisePosition : NetworkDetectedNoisePosition;
    public float Suspicion => UsesLocalState ? localSuspicion : NetworkSuspicion;
    public bool HasDetectedNoise => UsesLocalState ? localHasDetectedNoise : NetworkHasDetectedNoise;
    public NoiseData.NoiseType LastNoiseType => (NoiseData.NoiseType)(UsesLocalState ? localLastNoiseTypeRaw : NetworkLastNoiseTypeRaw);
    public float LastNoiseIntensity => UsesLocalState ? localLastNoiseIntensity : NetworkLastNoiseIntensity;
    public float LastNoiseTime => UsesLocalState ? localLastNoiseTime : NetworkLastNoiseTime;
    public Vector3 LastConfirmedNoisePosition => UsesLocalState ? localLastConfirmedNoisePosition : NetworkLastConfirmedNoisePosition;
    public Vector3 CurrentInvestigationPosition => UsesLocalState ? localCurrentInvestigationPosition : NetworkCurrentInvestigationPosition;
    public bool IsLocalSimulationActive => isLocalSimulationActive;
    private bool UsesLocalState => isLocalSimulationActive || !isNetworkSpawned;

    private void Awake()
    {
        EnsureRuntimeInitialized();
    }

    public bool EnsureRuntimeInitialized()
    {
        if (isRuntimeInitialized)
            return true;

        Animator = GetComponent<Animator>();
        Agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<EnemyHealth>();
        AudioController = GetComponent<EnemyAudioController>();
        AnimationEventHandler = GetComponentInChildren<EnemyAnimationEventHandler>();
        AttackColliders = GetComponentsInChildren<EnemyAttackCollider>();

        if (data == null || Animator == null || Agent == null || Health == null || AnimationEventHandler == null)
        {
            Debug.LogError("[EnemyAI] Local simulation requires EnemyData, Animator, NavMeshAgent, EnemyHealth, and EnemyAnimationEventHandler.", this);
            return false;
        }

        PatrolCenter = transform.position;

        StateMachine = new EnemyStateMachine();
        IdleState = new EnemyIdleState(this, StateMachine);
        PatrolState = new EnemyPatrolState(this, StateMachine);
        AlertState = new EnemyAlertState(this, StateMachine);
        ChaseState = new EnemyChaseState(this, StateMachine);
        SearchState = new EnemySearchState(this, StateMachine);
        AttackState = new EnemyAttackState(this, StateMachine);
        HitState = new EnemyHitState(this, StateMachine);
        DeadState = new EnemyDeadState(this, StateMachine);

        isRuntimeInitialized = true;
        return true;
    }

    public override void Spawned()
    {
        if (!EnsureRuntimeInitialized())
            return;

        isNetworkSpawned = true;
        isLocalSimulationActive = false;
        StateMachine.Initialize(IdleState);

        if (HasStateAuthority)
        {
            PatrolCenter = transform.position;
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
            NetworkDetectedNoisePosition = Vector3.zero;
            NetworkSuspicion = 0f;
            NetworkHasDetectedNoise = false;
            NetworkLastNoiseTypeRaw = (int)NoiseData.NoiseType.Idle;
            NetworkLastNoiseIntensity = 0f;
            NetworkLastNoiseTime = 0f;
            NetworkLastConfirmedNoisePosition = Vector3.zero;
            NetworkCurrentInvestigationPosition = Vector3.zero;
            nextAttackAllowedTime = 0f;
            nextJumpAttackDecisionTime = 0f;
            hasPreparedJumpAttack = false;
            ResetNoiseMemory();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        isNetworkSpawned = false;
    }

    public void StartLocalSimulation()
    {
        if (!EnsureRuntimeInitialized())
            return;

        if (isLocalSimulationActive)
            return;

        isLocalSimulationActive = true;
        PatrolCenter = transform.position;
        localDetectedNoisePosition = Vector3.zero;
        localSuspicion = 0f;
        localHasDetectedNoise = false;
        localLastNoiseTypeRaw = (int)NoiseData.NoiseType.Idle;
        localLastNoiseIntensity = 0f;
        localLastNoiseTime = 0f;
        localLastConfirmedNoisePosition = Vector3.zero;
        localCurrentInvestigationPosition = Vector3.zero;
        nextAttackAllowedTime = 0f;
        nextJumpAttackDecisionTime = 0f;
        hasPreparedJumpAttack = false;
        ResetNoiseMemory();
        StateMachine.Initialize(IdleState);
    }

    public void StopLocalSimulation()
    {
        isLocalSimulationActive = false;
    }

    public void TickLocalSimulation(float deltaTime)
    {
        if (!isLocalSimulationActive || StateMachine?.CurrentState == null)
            return;

        StateMachine.CurrentState.LogicUpdate();
        StateMachine.CurrentState.PhysicsUpdate();
        UpdateNoiseMemory(deltaTime);
        UpdateSuspicion(deltaTime);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        StateMachine.CurrentState.LogicUpdate();
        StateMachine.CurrentState.PhysicsUpdate();
        float deltaTime = Runner != null ? Runner.DeltaTime : Time.deltaTime;
        UpdateNoiseMemory(deltaTime);
        UpdateSuspicion(deltaTime);

        NetworkPosition = transform.position;
        NetworkRotation = transform.rotation;
        SyncAnimatorSnapshot();
    }

    public override void Render()
    {
        if (HasStateAuthority || isLocalSimulationActive || !isNetworkSpawned)
            return;

        transform.SetPositionAndRotation(NetworkPosition, NetworkRotation);
        Animator.SetFloat("Speed", NetworkAnimSpeed, 0.2f, Time.deltaTime);
        Animator.SetFloat("Angle", NetworkAnimAngle, 0.2f, Time.deltaTime);
        Animator.SetBool("IsAlert", NetworkAnimIsAlert);
        Animator.SetInteger("WaitIndex", NetworkAnimWaitIndex);
        Animator.SetInteger("HitIndex", NetworkAnimHitIndex);
        Animator.SetInteger("AttackIndex", NetworkAnimAttackIndex);

        if (lastAttackTriggerCount != NetworkAttackTriggerCount) { Animator.SetTrigger("Attack"); lastAttackTriggerCount = NetworkAttackTriggerCount; }
        if (lastHitTriggerCount != NetworkHitTriggerCount) { Animator.SetTrigger("Hit"); lastHitTriggerCount = NetworkHitTriggerCount; }
        if (lastDeadTriggerCount != NetworkDeadTriggerCount) { Animator.SetTrigger("Dead"); lastDeadTriggerCount = NetworkDeadTriggerCount; }
        if (lastTurnLeftTriggerCount != NetworkTurnLeftTriggerCount) { Animator.SetTrigger("TurnLeft"); lastTurnLeftTriggerCount = NetworkTurnLeftTriggerCount; }
        if (lastTurnRightTriggerCount != NetworkTurnRightTriggerCount) { Animator.SetTrigger("TurnRight"); lastTurnRightTriggerCount = NetworkTurnRightTriggerCount; }
        if (lastAnimStateChangeCount != NetworkAnimStateChangeCount)
        {
            ApplyNetworkAnimState();
            lastAnimStateChangeCount = NetworkAnimStateChangeCount;
        }

    }

    private void SyncAnimatorSnapshot()
    {
        NetworkAnimSpeed = Animator.GetFloat("Speed");
        NetworkAnimAngle = Animator.GetFloat("Angle");
        NetworkAnimIsAlert = Animator.GetBool("IsAlert");
        NetworkAnimWaitIndex = Animator.GetInteger("WaitIndex");
        NetworkAnimHitIndex = Animator.GetInteger("HitIndex");
        NetworkAnimAttackIndex = Animator.GetInteger("AttackIndex");

    }

    public void NotifyAnimatorTrigger(string triggerName)
    {
        if (isLocalSimulationActive)
            return;

        if (!isNetworkSpawned || !HasStateAuthority)
            return;

        switch (triggerName)
        {
            case "Attack": NetworkAttackTriggerCount++; break;
            case "Hit": NetworkHitTriggerCount++; break;
            case "Dead": NetworkDeadTriggerCount++; break;
            case "TurnLeft": NetworkTurnLeftTriggerCount++; break;
            case "TurnRight": NetworkTurnRightTriggerCount++; break;
        }
    }

    public void NotifyAnimatorState(byte stateId)
    {
        if (isLocalSimulationActive)
            return;

        if (!isNetworkSpawned || !HasStateAuthority)
            return;

        NetworkAnimStateId = stateId;
        NetworkAnimStateChangeCount++;
    }

    public void RequestStateSound(EnemySoundCue cue)
    {
        if (AudioController == null)
            return;

        if (isLocalSimulationActive || !isNetworkSpawned)
        {
            AudioController.Play(cue);
            return;
        }

        if (!HasStateAuthority)
            return;

        RpcPlayEnemySound((byte)cue);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayEnemySound(byte cueId)
    {
        if (AudioController == null)
        {
            AudioController = GetComponent<EnemyAudioController>();
        }

        AudioController?.Play((EnemySoundCue)cueId);
    }

    private void ApplyNetworkAnimState()
    {
        switch (NetworkAnimStateId)
        {
            case 1:
                Animator.CrossFade("Locomotion", 0.2f);
                break;
            case 2:
                Animator.CrossFade("Wait1", 0.2f);
                break;
            case 3:
                Animator.CrossFade("Wait2", 0.2f);
                break;
        }
    }

    public void SetPatrolCenter(Vector3 newCenter)
    {
        PatrolCenter = newCenter;
    }

    public void ApplySpawnPose(Vector3 position, Quaternion rotation)
    {
        if (!EnsureRuntimeInitialized())
            return;

        PatrolCenter = position;

        if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
        {
            Agent.Warp(position);
        }
        else
        {
            transform.position = position;
        }

        transform.rotation = rotation;

        if (isNetworkSpawned && HasStateAuthority)
        {
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
            NetworkCurrentInvestigationPosition = transform.position;
        }
    }

    public void SetCurrentInvestigationPosition(Vector3 position)
    {
        if (UsesLocalState)
        {
            localCurrentInvestigationPosition = position;
            return;
        }

        NetworkCurrentInvestigationPosition = position;
    }

    public bool TrySetDestination(Vector3 targetPosition)
    {
        return TrySetDestination(targetPosition, out _);
    }

    public bool TrySetDestination(Vector3 targetPosition, out Vector3 resolvedDestination)
    {
        resolvedDestination = targetPosition;

        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            return false;

        float sampleRange = data != null ? data.navMeshSampleRange : 2f;
        if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, sampleRange, NavMesh.AllAreas))
            return false;

        resolvedDestination = hit.position;
        Agent.SetDestination(resolvedDestination);
        return true;
    }

    public bool TryChangeStateBySuspicion()
    {
        if (Suspicion >= data.chaseThreshold)
        {
            StateMachine.ChangeState(ChaseState);
            return true;
        }

        if (Suspicion >= data.searchThreshold)
        {
            StateMachine.ChangeState(SearchState);
            return true;
        }

        if (Suspicion >= data.alertThreshold)
        {
            StateMachine.ChangeState(AlertState);
            return true;
        }

        return false;
    }

    public void RegisterHitReaction()
    {
        if (StateMachine?.CurrentState == DeadState)
            return;

        ApplyHitAwareness();

        if (StateMachine.CurrentState == AttackState && AttackState.BlocksHitReaction)
            return;

        if (StateMachine.CurrentState == HitState)
        {
            HitState.RestartReaction();
            return;
        }

        InterruptedStateBeforeHit = StateMachine.CurrentState;
        StateMachine.ChangeState(HitState);
    }

    public EnemyState ConsumeInterruptedStateBeforeHit()
    {
        EnemyState interruptedState = InterruptedStateBeforeHit;
        InterruptedStateBeforeHit = null;
        return interruptedState;
    }

    public bool HasMeaningfullyNewNoise(Vector3 previousNoisePosition)
    {
        return HasDetectedNoise &&
               Vector3.Distance(DetectedNoisePosition, previousNoisePosition) > data.newNoisePositionThreshold;
    }

    private void ApplyHitAwareness()
    {
        bool hadKnownNoise = HasDetectedNoise;
        float gain = data.hitSuspicionGain;
        float nextSuspicion = Mathf.Max(data.hitMinimumSuspicion, Suspicion + gain);
        if (!hadKnownNoise)
        {
            nextSuspicion = Mathf.Min(nextSuspicion, data.searchThreshold);
        }

        nextSuspicion = Mathf.Clamp(nextSuspicion, 0f, 100f);

        Vector3 reactionPosition = hadKnownNoise ? DetectedNoisePosition : transform.position;
        int memoryIndex = AddOrMergeNoiseMemory(
            reactionPosition,
            reactionPosition,
            Mathf.Clamp01(gain / 100f),
            NoiseData.NoiseType.Pain,
            false,
            gain);
        ActivateNoiseMemory(memoryIndex, forceLock: true);
        lastNoiseTime = Time.time;
        SetSuspicion(nextSuspicion);
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseIntensity)
    {
        OnNoiseDetected(noisePosition, noiseIntensity, NoiseData.NoiseType.Idle, false);
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseIntensity, NoiseData.NoiseType noiseType, bool isObstructed)
    {
        if (!isLocalSimulationActive && (!isNetworkSpawned || !HasStateAuthority))
            return;

        float gain = noiseIntensity * data.suspicionGainAmount * data.suspicionSensitivity;
        Vector3 estimatedPosition = EstimateNoisePosition(noisePosition, noiseIntensity, isObstructed);
        SetSuspicion(Mathf.Clamp(Suspicion + gain, 0f, 100f));
        lastNoiseTime = Time.time;

        int memoryIndex = AddOrMergeNoiseMemory(noisePosition, estimatedPosition, noiseIntensity, noiseType, isObstructed, gain);
        if (memoryIndex == activeNoiseMemoryIndex)
        {
            ActivateNoiseMemory(memoryIndex, forceLock: false);
            return;
        }

        TryRetargetNoiseMemory(!HasDetectedNoise);
    }

    public void LookDetectedNoisePosition()
    {
        Vector3 targetDirection = DetectedNoisePosition - transform.position;
        targetDirection.y = 0f;

        if (targetDirection == Vector3.zero) return;

        float targetY = Quaternion.LookRotation(targetDirection).eulerAngles.y;
        float smoothTime = Mathf.Max(0.05f, 1.5f / data.rotationSpeed);
        float newY = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetY, ref rotationVelocity, smoothTime);
        transform.rotation = Quaternion.Euler(0f, newY, 0f);
    }

    public bool IsPlayerInAttackRadius()
    {
        return TryGetAttackTargetDirection(out _, true, data.attackRadius);
    }

    public bool CanStartAttack()
    {
        if (Time.time < nextAttackAllowedTime)
            return false;

        if (IsPlayerInAttackRadius())
            return true;

        return TryPrepareJumpAttack();
    }

    public void RegisterAttackStarted()
    {
        nextAttackAllowedTime = Time.time + Mathf.Max(0f, data.attackCooldown);
    }

    public bool TryGetAttackTargetRotation(out Quaternion targetRotation)
    {
        targetRotation = transform.rotation;

        if (!TryGetAttackTargetDirection(out Vector3 targetDirection, true, data.attackRadius) &&
            !TryGetAttackTargetDirection(out targetDirection, false, data.attackRadius))
        {
            return false;
        }

        targetRotation = Quaternion.LookRotation(targetDirection);
        return true;
    }

    public bool TryConsumePreparedJumpAttack(out Quaternion attackRotation, out Vector3 landingPosition)
    {
        attackRotation = preparedJumpAttackRotation;
        landingPosition = preparedJumpAttackLandingPosition;

        if (!hasPreparedJumpAttack)
            return false;

        hasPreparedJumpAttack = false;
        return true;
    }

    public bool TrySnapAgentToNearestNavMesh(float sampleRange)
    {
        if (Agent == null)
            return false;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleRange, NavMesh.AllAreas))
            return false;

        if (!Agent.enabled)
        {
            transform.position = hit.position;
            Agent.enabled = true;
        }

        Agent.Warp(hit.position);
        transform.position = hit.position;
        return true;
    }

    private bool TryPrepareJumpAttack()
    {
        if (Suspicion < data.chaseThreshold)
            return false;

        if (hasPreparedJumpAttack)
            return true;

        float now = Time.time;
        if (now < nextJumpAttackDecisionTime)
            return false;

        nextJumpAttackDecisionTime = now + Mathf.Max(0f, data.jumpAttackDecisionInterval);
        if (Random.value > data.jumpAttackChance)
            return false;

        if (!TryGetAttackTargetInfo(
                Mathf.Max(data.jumpAttackMaxDistance, data.attackRadius),
                true,
                data.jumpAttackAngle,
                out Vector3 targetPosition,
                out Vector3 targetDirection,
                out float targetDistance))
        {
            return false;
        }

        if (targetDistance < data.jumpAttackMinDistance || targetDistance > data.jumpAttackMaxDistance)
            return false;

        if (!TryResolveJumpAttackLanding(targetPosition, out Vector3 landingPosition))
            return false;

        preparedJumpAttackRotation = Quaternion.LookRotation(targetDirection);
        preparedJumpAttackLandingPosition = landingPosition;
        hasPreparedJumpAttack = true;
        return true;
    }

    private bool TryResolveJumpAttackLanding(Vector3 targetPosition, out Vector3 landingPosition)
    {
        landingPosition = targetPosition;
        float sampleRange = Mathf.Max(0.1f, data.jumpAttackLandingSampleRange);
        if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit landingHit, sampleRange, NavMesh.AllAreas))
            return false;

        Vector3 start = transform.position;
        Vector3 end = landingHit.position;
        if (NavMesh.Raycast(start, end, out _, NavMesh.AllAreas))
            return false;

        if (data.jumpAttackObstacleMask.value != 0)
        {
            Vector3 castStart = start + Vector3.up * Mathf.Max(0f, data.jumpAttackObstacleHeight);
            Vector3 castEnd = end + Vector3.up * Mathf.Max(0f, data.jumpAttackObstacleHeight);
            Vector3 castDirection = castEnd - castStart;
            float castDistance = castDirection.magnitude;
            if (castDistance > 0.01f &&
                Physics.SphereCast(
                    castStart,
                    Mathf.Max(0.01f, data.jumpAttackObstacleRadius),
                    castDirection.normalized,
                    out _,
                    castDistance,
                    data.jumpAttackObstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }
        }

        landingPosition = landingHit.position;
        return true;
    }

    private bool TryGetAttackTargetDirection(out Vector3 targetDirection, bool requireAttackAngle, float radius)
    {
        bool foundTarget = TryGetAttackTargetInfo(
            radius,
            requireAttackAngle,
            data.attackAngle,
            out _,
            out targetDirection,
            out _);

        return foundTarget;
    }

    private bool TryGetAttackTargetInfo(
        float radius,
        bool requireAttackAngle,
        float angle,
        out Vector3 targetPosition,
        out Vector3 targetDirection,
        out float targetDistance)
    {
        targetPosition = Vector3.zero;
        targetDirection = Vector3.zero;
        targetDistance = 0f;
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, attackCheckBuffer);
        float bestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            if (!attackCheckBuffer[i].CompareTag("Player")) continue;

            Vector3 dir = attackCheckBuffer[i].transform.position - transform.position;
            dir.y = 0f;
            float sqrDistance = dir.sqrMagnitude;
            if (sqrDistance <= 0.0001f)
                continue;

            if (requireAttackAngle && Vector3.Angle(transform.forward, dir) > angle * 0.5f)
                continue;

            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            targetPosition = attackCheckBuffer[i].transform.position;
            targetDirection = dir.normalized;
        }

        if (bestSqrDistance >= float.PositiveInfinity)
            return false;

        targetDistance = Mathf.Sqrt(bestSqrDistance);
        return true;
    }

    private void UpdateSuspicion(float deltaTime)
    {
        if (StateMachine.CurrentState == ChaseState
            || StateMachine.CurrentState == AttackState
            || StateMachine.CurrentState == DeadState)
            return;

        if (Time.time < lastNoiseTime + data.suspicionReduceDelay)
            return;

        if (Suspicion > 0f)
        {
            SetSuspicion(Mathf.Max(0f, Suspicion - data.suspicionReduceRate * deltaTime));
            if (Suspicion <= 0f)
            {
                SetSuspicion(0f);
                SetDetectedNoise(Vector3.zero, false);
                ResetNoiseMemory();
            }
        }
    }

    private void UpdateNoiseMemory(float deltaTime)
    {
        float now = Time.time;
        float duration = data != null ? data.noiseMemoryDuration : 0f;
        int capacity = GetNoiseMemoryCapacity();

        for (int i = 0; i < capacity; i++)
        {
            if (!noiseMemorySlots[i].isActive)
                continue;

            if (duration > 0f && now - noiseMemorySlots[i].lastHeardTime > duration)
            {
                noiseMemorySlots[i].isActive = false;
                if (activeNoiseMemoryIndex == i)
                {
                    activeNoiseMemoryIndex = -1;
                }
            }
        }

        TryRetargetNoiseMemory(activeNoiseMemoryIndex < 0);
    }

    private int AddOrMergeNoiseMemory(
        Vector3 sourcePosition,
        Vector3 estimatedPosition,
        float intensity,
        NoiseData.NoiseType noiseType,
        bool isObstructed,
        float gain)
    {
        int capacity = GetNoiseMemoryCapacity();
        int bestMergeIndex = -1;
        float mergeRadius = data != null ? Mathf.Max(0f, data.repeatedNoiseMergeRadius) : 0f;

        if (mergeRadius > 0f)
        {
            for (int i = 0; i < capacity; i++)
            {
                if (!noiseMemorySlots[i].isActive)
                    continue;

                float sourceDistance = Vector3.Distance(noiseMemorySlots[i].sourcePosition, sourcePosition);
                float estimatedDistance = Vector3.Distance(noiseMemorySlots[i].estimatedPosition, estimatedPosition);
                if (sourceDistance <= mergeRadius || estimatedDistance <= mergeRadius)
                {
                    bestMergeIndex = i;
                    break;
                }
            }
        }

        if (bestMergeIndex < 0)
        {
            bestMergeIndex = FindNoiseMemoryWriteIndex(capacity);
        }

        NoiseMemorySlot slot = noiseMemorySlots[bestMergeIndex];
        bool wasActive = slot.isActive;
        float repeatedBonus = wasActive && data != null ? data.repeatedNoiseBonus : 0f;

        slot.isActive = true;
        slot.sourcePosition = sourcePosition;
        slot.estimatedPosition = estimatedPosition;
        slot.noiseType = noiseType;
        slot.intensity = Mathf.Max(slot.intensity, intensity);
        slot.score = Mathf.Max(slot.score, gain) + repeatedBonus;
        slot.lastHeardTime = Time.time;
        slot.isObstructed = isObstructed;
        slot.repeatCount = wasActive ? slot.repeatCount + 1 : 1;
        noiseMemorySlots[bestMergeIndex] = slot;

        return bestMergeIndex;
    }

    private int FindNoiseMemoryWriteIndex(int capacity)
    {
        int lowestScoreIndex = 0;
        float lowestScore = float.PositiveInfinity;

        for (int i = 0; i < capacity; i++)
        {
            if (!noiseMemorySlots[i].isActive)
                return i;

            float score = CalculateNoiseMemoryScore(i);
            if (score < lowestScore)
            {
                lowestScore = score;
                lowestScoreIndex = i;
            }
        }

        if (activeNoiseMemoryIndex == lowestScoreIndex)
        {
            activeNoiseMemoryIndex = -1;
        }

        return lowestScoreIndex;
    }

    private void TryRetargetNoiseMemory(bool force)
    {
        float now = Time.time;
        if (!force && now < nextNoiseRetargetTime)
            return;

        nextNoiseRetargetTime = now + Mathf.Max(0.05f, data.noiseRetargetInterval);

        int bestIndex = FindBestNoiseMemoryIndex();
        if (bestIndex < 0)
            return;

        if (force || activeNoiseMemoryIndex < 0 || !noiseMemorySlots[activeNoiseMemoryIndex].isActive)
        {
            ActivateNoiseMemory(bestIndex, forceLock: true);
            return;
        }

        if (bestIndex == activeNoiseMemoryIndex)
        {
            ActivateNoiseMemory(bestIndex, forceLock: false);
            return;
        }

        if (now < noiseRetargetLockedUntil)
            return;

        float currentScore = CalculateNoiseMemoryScore(activeNoiseMemoryIndex);
        float bestScore = CalculateNoiseMemoryScore(bestIndex);
        if (bestScore >= currentScore + data.noiseRetargetScoreMargin)
        {
            ActivateNoiseMemory(bestIndex, forceLock: true);
        }
    }

    private int FindBestNoiseMemoryIndex()
    {
        int capacity = GetNoiseMemoryCapacity();
        int bestIndex = -1;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < capacity; i++)
        {
            if (!noiseMemorySlots[i].isActive)
                continue;

            float score = CalculateNoiseMemoryScore(i);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private float CalculateNoiseMemoryScore(int index)
    {
        if (index < 0 || index >= noiseMemorySlots.Length || !noiseMemorySlots[index].isActive)
            return float.NegativeInfinity;

        NoiseMemorySlot slot = noiseMemorySlots[index];
        float age = Mathf.Max(0f, Time.time - slot.lastHeardTime);
        float score = slot.score - age * data.noiseMemoryScoreDecayRate;
        if (slot.isObstructed)
        {
            score -= data.obstructedNoiseScorePenalty;
        }

        return score;
    }

    private void ActivateNoiseMemory(int index, bool forceLock)
    {
        if (index < 0 || index >= noiseMemorySlots.Length || !noiseMemorySlots[index].isActive)
            return;

        NoiseMemorySlot slot = noiseMemorySlots[index];
        activeNoiseMemoryIndex = index;
        SetDetectedNoise(slot.estimatedPosition, true);
        SetNoiseMemory(slot.sourcePosition, slot.estimatedPosition, slot.intensity, slot.noiseType);

        if (forceLock)
        {
            noiseRetargetLockedUntil = Time.time + Mathf.Max(0f, data.noiseRetargetMinStickTime);
        }
    }

    private void ResetNoiseMemory()
    {
        for (int i = 0; i < noiseMemorySlots.Length; i++)
        {
            noiseMemorySlots[i] = default;
        }

        activeNoiseMemoryIndex = -1;
        nextNoiseRetargetTime = 0f;
        noiseRetargetLockedUntil = 0f;
    }

    private int GetNoiseMemoryCapacity()
    {
        return data != null ? Mathf.Clamp(data.noiseMemoryCapacity, 1, MaxNoiseMemorySlots) : MaxNoiseMemorySlots;
    }

    private Vector3 EstimateNoisePosition(Vector3 sourcePosition, float noiseIntensity, bool isObstructed)
    {
        float uncertainty = Mathf.Pow(1f - Mathf.Clamp01(noiseIntensity), data.noisePositionErrorPower);
        float errorRadius = data.maxNoisePositionError * uncertainty;
        if (isObstructed)
        {
            errorRadius += data.obstructedNoisePositionErrorBonus;
        }

        if (errorRadius <= 0.01f)
            return sourcePosition;

        Vector2 offset = Random.insideUnitCircle * errorRadius;
        return sourcePosition + new Vector3(offset.x, 0f, offset.y);
    }

    private void SetNoiseMemory(Vector3 sourcePosition, Vector3 estimatedPosition, float intensity, NoiseData.NoiseType noiseType)
    {
        if (isLocalSimulationActive)
        {
            localLastNoiseTypeRaw = (int)noiseType;
            localLastNoiseIntensity = intensity;
            localLastNoiseTime = Time.time;
            localLastConfirmedNoisePosition = sourcePosition;
            localCurrentInvestigationPosition = estimatedPosition;
            return;
        }

        NetworkLastNoiseTypeRaw = (int)noiseType;
        NetworkLastNoiseIntensity = intensity;
        NetworkLastNoiseTime = Time.time;
        NetworkLastConfirmedNoisePosition = sourcePosition;
        NetworkCurrentInvestigationPosition = estimatedPosition;
    }

    private void SetDetectedNoise(Vector3 position, bool hasDetectedNoise)
    {
        if (isLocalSimulationActive)
        {
            localDetectedNoisePosition = position;
            localHasDetectedNoise = hasDetectedNoise;
            return;
        }

        NetworkDetectedNoisePosition = position;
        NetworkHasDetectedNoise = hasDetectedNoise;
    }

    private void SetSuspicion(float suspicion)
    {
        if (isLocalSimulationActive)
        {
            localSuspicion = suspicion;
            return;
        }

        NetworkSuspicion = suspicion;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        string stateName = GetDebugStateName();
        string suspicionStateName = GetDebugSuspicionStateName();
        Handles.Label(transform.position + Vector3.up * 2.2f,
            $"State: {stateName}\nSuspicion: {Suspicion:F0}% ({suspicionStateName})");
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

#if UNITY_EDITOR
        Handles.color = new Color(0.2f, 0.5f, 1f, 1f);
        Handles.DrawWireDisc(PatrolCenter, Vector3.up, data.patrolRadius);

        Handles.color = new Color(1f, 0.2f, 0.2f, 1f);
        float halfAngle = data.attackAngle * 0.5f;
        Vector3 leftDir = Quaternion.Euler(0f, -halfAngle, 0f) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0f, halfAngle, 0f) * transform.forward;
        Handles.DrawLine(transform.position, transform.position + leftDir * data.attackRadius);
        Handles.DrawLine(transform.position, transform.position + rightDir * data.attackRadius);
        Handles.DrawWireArc(transform.position, Vector3.up, leftDir, data.attackAngle, data.attackRadius);

        DrawNoiseDebugGizmos();
#endif
    }

#if UNITY_EDITOR
    private void DrawNoiseDebugGizmos()
    {
        if (!HasDetectedNoise)
            return;

        Vector3 enemyPosition = transform.position;
        Vector3 confirmedPosition = LastConfirmedNoisePosition;
        Vector3 estimatedPosition = CurrentInvestigationPosition;
        bool hasConfirmedPosition = confirmedPosition != Vector3.zero;
        bool hasEstimatedPosition = estimatedPosition != Vector3.zero;

        Handles.color = new Color(1f, 0.65f, 0.1f, 1f);
        Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.8f);
        if (hasEstimatedPosition)
        {
            Gizmos.DrawSphere(estimatedPosition + Vector3.up * 0.08f, 0.18f);
            Handles.DrawDottedLine(enemyPosition + Vector3.up * 0.2f, estimatedPosition + Vector3.up * 0.2f, 4f);
            Handles.Label(
                estimatedPosition + Vector3.up * 0.6f,
                $"Investigating\n{LastNoiseType} / Intensity {LastNoiseIntensity:F2}");
        }

        Handles.color = new Color(0.2f, 0.9f, 1f, 1f);
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        if (hasConfirmedPosition)
        {
            Gizmos.DrawWireSphere(confirmedPosition + Vector3.up * 0.08f, 0.35f);
            Handles.Label(confirmedPosition + Vector3.up * 0.9f, "Noise Source");
        }

        if (hasConfirmedPosition && hasEstimatedPosition)
        {
            Handles.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            Handles.DrawDottedLine(confirmedPosition + Vector3.up * 0.15f, estimatedPosition + Vector3.up * 0.15f, 3f);
        }
    }

    private string GetDebugStateName()
    {
        EnemyState currentState = StateMachine?.CurrentState;
        if (currentState == null)
            return Application.isPlaying ? "None" : "Edit Mode";

        string typeName = currentState.GetType().Name;
        return typeName.Replace("Enemy", "").Replace("State", "");
    }

    private string GetDebugSuspicionStateName()
    {
        if (data == null)
            return "No Data";

        float suspicion = Suspicion;
        if (suspicion >= data.chaseThreshold)
            return "Chase";

        if (suspicion >= data.searchThreshold)
            return "Search";

        if (suspicion >= data.lookThreshold)
            return "Look";

        if (suspicion >= data.alertThreshold)
            return "Alert";

        return "Calm";
    }
#endif
}
