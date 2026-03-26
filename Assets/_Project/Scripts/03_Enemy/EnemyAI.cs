using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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
    public float DetectionRadius => data != null ? data.detectionRadius : 0f;

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

    public void SetSuspicionLevel(float value)
    {
        SuspicionLevel = Mathf.Clamp(value, 0f, 100f);
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseRadius)
    {
        float distance = Mathf.Max(0.01f, Vector3.Distance(transform.position, noisePosition));
        if (distance > noiseRadius + data.detectionRadius) return;

        DetectedNoisePosition = noisePosition;

        float gain = data.suspicionGainAmount * (noiseRadius / distance) * data.suspicionSensitivity;
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
            if (hit.CompareTag("Player"))
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
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f,
            $"Suspicion: {SuspicionLevel:F0}%");
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 1f);
        UnityEditor.Handles.DrawWireDisc(PatrolCenter, Vector3.up, data.patrolRadius);

        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.1f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, data.detectionRadius);

        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, data.attackRadius);
    }
}