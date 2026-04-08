using System.Collections;
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
    private Coroutine reduceCoroutine;
    private float rotationVelocity;

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
    public float SuspicionLevel { get; private set; }

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
    }

    private void FixedUpdate()
    {
        StateMachine.CurrentState.PhysicsUpdate();
    }

    public void SetPatrolCenter(Vector3 newCenter)
    {
        PatrolCenter = newCenter;
    }

    public void SetSuspicionLevel(float value)
    {
        SuspicionLevel = Mathf.Clamp(value, 0f, 100f);
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseIntensity)
    {
        DetectedNoisePosition = noisePosition;

        float gain = noiseIntensity * data.suspicionGainAmount * data.suspicionSensitivity;
        SuspicionLevel = Mathf.Clamp(SuspicionLevel + gain, 0f, 100f);

        if (reduceCoroutine != null)
            StopCoroutine(reduceCoroutine);
        reduceCoroutine = StartCoroutine(SuspicionReduceCoroutine());
    }

    public bool IsPlayerInAttackRadius()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, data.attackRadius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            Vector3 dir = hit.transform.position - transform.position;
            dir.y = 0f;
            if (Vector3.Angle(transform.forward, dir) <= data.attackAngle * 0.5f)
                return true;
        }
        return false;
    }

    public void LookAtDetectedNoisePosition()
    {
        Vector3 targetDirection = DetectedNoisePosition - transform.position;
        targetDirection.y = 0f;

        if (targetDirection == Vector3.zero) return;

        float targetY = Quaternion.LookRotation(targetDirection).eulerAngles.y;
        float smoothTime = Mathf.Max(0.05f, 1.5f / data.rotationSpeed);
        float newY = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetY, ref rotationVelocity, smoothTime);
        transform.rotation = Quaternion.Euler(0f, newY, 0f);
    }

    private IEnumerator SuspicionReduceCoroutine()
    {
        yield return new WaitForSeconds(data.suspicionReduceDelay);

        while (SuspicionLevel > 0f)
        {
            if (StateMachine.CurrentState != ChaseState)
                SuspicionLevel = Mathf.Max(0f, SuspicionLevel - data.suspicionReduceRate * Time.deltaTime);
            yield return null;
        }

        reduceCoroutine = null;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Handles.Label(transform.position + Vector3.up * 2.2f,
            $"Suspicion: {SuspicionLevel:F0}%");
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
