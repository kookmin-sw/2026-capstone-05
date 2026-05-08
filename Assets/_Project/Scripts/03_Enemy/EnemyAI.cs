using Fusion;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(NetworkObject))]
public class EnemyAI : NetworkBehaviour, INoiseListener
{
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
    private Vector3 localDetectedNoisePosition;
    private float localSuspicion;
    private bool localHasDetectedNoise;

    [Networked] private Vector3 NetworkPosition { get; set; }
    [Networked] private Quaternion NetworkRotation { get; set; }
    [Networked] private Vector3 NetworkDetectedNoisePosition { get; set; }
    [Networked] private float NetworkSuspicion { get; set; }
    [Networked] private NetworkBool NetworkHasDetectedNoise { get; set; }

    [Networked] private float NetworkAnimSpeed { get; set; }
    [Networked] private float NetworkAnimAngle { get; set; }
    [Networked] private NetworkBool NetworkAnimIsAlert { get; set; }
    [Networked] private int NetworkAnimWaitIndex { get; set; }
    [Networked] private int NetworkAnimHitIndex { get; set; }
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
    public EnemyAnimationEventHandler AnimationEventHandler { get; private set; }
    public EnemyAttackCollider[] AttackColliders { get; private set; }
    private int lastAnimStateChangeCount;
    private bool isInitializedLocally;

    // Properties: States
    public EnemyIdleState IdleState { get; private set; }
    public EnemyPatrolState PatrolState { get; private set; }
    public EnemyAlertState AlertState { get; private set; }
    public EnemyChaseState ChaseState { get; private set; }
    public EnemySearchState SearchState { get; private set; }
    public EnemyAttackState AttackState { get; private set; }
    public EnemyHitState HitState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }

    // Properties: Gameplay
    public Vector3 PatrolCenter { get; private set; }
    public Vector3 DetectedNoisePosition => IsNetworkStateReady ? NetworkDetectedNoisePosition : localDetectedNoisePosition;
    public float Suspicion => IsNetworkStateReady ? NetworkSuspicion : localSuspicion;
    public bool HasDetectedNoise => IsNetworkStateReady ? NetworkHasDetectedNoise : localHasDetectedNoise;
    private bool IsNetworkStateReady => Runner != null && Object != null && Object.IsValid;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<EnemyHealth>();
        AnimationEventHandler = GetComponentInChildren<EnemyAnimationEventHandler>();
        AttackColliders = GetComponentsInChildren<EnemyAttackCollider>();

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
    }


    private void Start()
    {
        if (Runner != null)
        {
            return;
        }

        InitializeForLocalOnlyMode();
    }

    public override void Spawned()
    {
        InitializeForLocalOnlyMode();

        if (HasStateAuthority)
        {
            PatrolCenter = transform.position;
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
            NetworkDetectedNoisePosition = Vector3.zero;
            NetworkSuspicion = 0f;
            NetworkHasDetectedNoise = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        TickStateMachine(Runner != null ? Runner.DeltaTime : Time.deltaTime);

        NetworkPosition = transform.position;
        NetworkRotation = transform.rotation;
        SyncAnimatorSnapshot();
    }

    private void Update()
    {
        if (Runner != null || !isInitializedLocally)
        {
            return;
        }

        TickStateMachine(Time.deltaTime);
    }

    private void TickStateMachine(float deltaTime)
    {
        StateMachine.CurrentState.LogicUpdate();
        StateMachine.CurrentState.PhysicsUpdate();
        UpdateSuspicion(deltaTime);
    }

    public override void Render()
    {
        if (HasStateAuthority)
            return;

        transform.SetPositionAndRotation(NetworkPosition, NetworkRotation);
        Animator.SetFloat("Speed", NetworkAnimSpeed, 0.2f, Time.deltaTime);
        Animator.SetFloat("Angle", NetworkAnimAngle, 0.2f, Time.deltaTime);
        Animator.SetBool("IsAlert", NetworkAnimIsAlert);
        Animator.SetInteger("WaitIndex", NetworkAnimWaitIndex);
        Animator.SetInteger("HitIndex", NetworkAnimHitIndex);

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

    }

    public void NotifyAnimatorTrigger(string triggerName)
    {
        if (!HasStateAuthority)
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
        if (!HasStateAuthority)
            return;

        NetworkAnimStateId = stateId;
        NetworkAnimStateChangeCount++;
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


    private void InitializeForLocalOnlyMode()
    {
        if (isInitializedLocally)
        {
            return;
        }

        StateMachine.Initialize(IdleState);
        PatrolCenter = transform.position;

        if (IsNetworkStateReady)
        {
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
            NetworkDetectedNoisePosition = Vector3.zero;
            NetworkSuspicion = 0f;
            NetworkHasDetectedNoise = false;
        }

        localDetectedNoisePosition = Vector3.zero;
        localSuspicion = 0f;
        localHasDetectedNoise = false;

        isInitializedLocally = true;
    }

    public void SetPatrolCenter(Vector3 newCenter)
    {
        PatrolCenter = newCenter;
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseIntensity)
    {
        if (IsNetworkStateReady)
        {
            NetworkDetectedNoisePosition = noisePosition;
            NetworkHasDetectedNoise = true;
        }
        else
        {
            localDetectedNoisePosition = noisePosition;
            localHasDetectedNoise = true;
        }

        lastNoiseTime = Time.time;

        float gain = noiseIntensity * data.suspicionGainAmount * data.suspicionSensitivity;
        if (IsNetworkStateReady)
        {
            NetworkSuspicion = Mathf.Clamp(NetworkSuspicion + gain, 0f, 100f);
        }
        else
        {
            localSuspicion = Mathf.Clamp(localSuspicion + gain, 0f, 100f);
        }
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
        if (!HasDetectedNoise) return false;

        int count = Physics.OverlapSphereNonAlloc(transform.position, data.attackRadius, attackCheckBuffer);
        for (int i = 0; i < count; i++)
        {
            if (!attackCheckBuffer[i].CompareTag("Player")) continue;
            Vector3 dir = attackCheckBuffer[i].transform.position - transform.position;
            dir.y = 0f;
            if (Vector3.Angle(transform.forward, dir) <= data.attackAngle * 0.5f)
                return true;
        }
        return false;
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
            float nextSuspicion = Mathf.Max(0f, Suspicion - data.suspicionReduceRate * deltaTime);
            if (IsNetworkStateReady)
            {
                NetworkSuspicion = nextSuspicion;
            }
            else
            {
                localSuspicion = nextSuspicion;
            }

            if (nextSuspicion <= 0f)
            {
                if (IsNetworkStateReady)
                {
                    NetworkSuspicion = 0f;
                    NetworkHasDetectedNoise = false;
                    NetworkDetectedNoisePosition = Vector3.zero;
                }
                else
                {
                    localSuspicion = 0f;
                    localHasDetectedNoise = false;
                    localDetectedNoisePosition = Vector3.zero;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Handles.Label(transform.position + Vector3.up * 2.2f,
            $"Suspicion: {Suspicion:F0}%");
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
#endif
    }
}
