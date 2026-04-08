using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemyAI : MonoBehaviour, INoiseListener
{
    // Serialized Fields
    [SerializeField] private EnemyData data;

    // Private Fields
    private float lastNoiseTime;
    private float rotationVelocity;
    private readonly Collider[] attackCheckBuffer = new Collider[16];

    // Properties: Core
    public EnemyData Data => data;
    public EnemyStateMachine StateMachine { get; private set; }
    public Animator Animator { get; private set; }
    public NavMeshAgent Agent { get; private set; }
    public EnemyHealth Health { get; private set; }
    public EnemyAnimationEventHandler AnimationEventHandler { get; private set; }
    public EnemyAttackCollider[] AttackColliders { get; private set; }

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
    public Vector3 DetectedNoisePosition { get; private set; }
    public float Suspicion { get; private set; }
    public bool HasDetectedNoise { get; private set; }

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
        StateMachine.Initialize(IdleState);
    }

    private void Update()
    {
        StateMachine.CurrentState.LogicUpdate();
        UpdateSuspicion();
    }

    private void FixedUpdate()
    {
        StateMachine.CurrentState.PhysicsUpdate();
    }

    public void SetPatrolCenter(Vector3 newCenter)
    {
        PatrolCenter = newCenter;
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseIntensity)
    {
        DetectedNoisePosition = noisePosition;
        HasDetectedNoise = true;
        lastNoiseTime = Time.time;

        float gain = noiseIntensity * data.suspicionGainAmount * data.suspicionSensitivity;
        Suspicion = Mathf.Clamp(Suspicion + gain, 0f, 100f);
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

    private void UpdateSuspicion()
    {
        if (StateMachine.CurrentState == ChaseState
            || StateMachine.CurrentState == AttackState
            || StateMachine.CurrentState == DeadState)
            return;

        if (Time.time < lastNoiseTime + data.suspicionReduceDelay)
            return;

        if (Suspicion > 0f)
        {
            Suspicion = Mathf.Max(0f, Suspicion - data.suspicionReduceRate * Time.deltaTime);
            if (Suspicion <= 0f)
            {
                Suspicion = 0f;
                HasDetectedNoise = false;
                DetectedNoisePosition = Vector3.zero;
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
