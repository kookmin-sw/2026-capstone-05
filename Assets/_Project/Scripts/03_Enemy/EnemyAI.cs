using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour, INoiseListener
{
    // Fields
    [SerializeField] private EnemyData data;

    // Properties
    public EnemyData Data => data;

    public EnemyStateMachine StateMachine { get; private set; }
    public Animator Animator { get; private set; }
    public NavMeshAgent Agent { get; private set; }
    public EnemyHealth Health { get; private set; }

    // States
    public EnemyIdleState IdleState { get; private set; }
    public EnemyPatrolState PatrolState { get; private set; }
    public EnemyAlertState AlertState { get; private set; }
    public EnemyChaseState ChaseState { get; private set; }
    public EnemySearchState SearchState { get; private set; }
    public EnemyAttackState AttackState { get; private set; }
    public EnemyHitState HitState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }

    // Patrol
    public Vector3 PatrolCenter { get; private set; }
    
    // Noise Suspicion
    public Vector3 DetectedNoisePosition { get; private set; }
    public float SuspicionLevel { get; private set; }
    public float DetectionRadius
    {
        get
        {
            if (data != null)
                return data.detectionRadius;
            else
                return 0f;
        }
    }
    private Coroutine reduceCoroutine;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<EnemyHealth>();

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

    public void LookAtDetectedNoisePosition()
    {
        Vector3 direction = DetectedNoisePosition - transform.position;
        direction.y = 0f;

        if (direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * data.rotationSpeed
        );
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseRadius)
    {
        DetectedNoisePosition = noisePosition;

        float distance = Vector3.Distance(transform.position, noisePosition);
        float ratio = Mathf.Clamp01(1f - distance / (noiseRadius + data.detectionRadius));
        float gain = data.suspicionGainAmount * ratio;
        SuspicionLevel = Mathf.Clamp(SuspicionLevel + gain, 0f, 100f);

        if (reduceCoroutine != null)
            StopCoroutine(reduceCoroutine);
        reduceCoroutine = StartCoroutine(ReduceSuspicionRoutine());
    }

    private IEnumerator ReduceSuspicionRoutine()
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

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 의심 수치 표시
        float t = SuspicionLevel / 100f;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f, 
            $"SuspicionLevel {SuspicionLevel:F0} / 100"
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        // 순찰 범위 (파란색)
        UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(PatrolCenter, Vector3.up, data.patrolRadius);
        UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 1f);
        UnityEditor.Handles.DrawWireDisc(PatrolCenter, Vector3.up, data.patrolRadius);

        // 소음 감지 범위 (노란색)
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.1f, 0.1f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, data.detectionRadius);
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.1f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, data.detectionRadius);

        // 공격 범위 (빨간색)
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.2f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, data.attackRadius);
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, data.attackRadius);
    }
#endif
}