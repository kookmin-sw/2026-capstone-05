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

    // Noise Suspicion
    public Vector3 DetectedNoisePosition { get; private set; }
    public float NoiseSuspicionLevel { get; private set; }
    public float NoiseDetectionRadius => data != null ? data.detectionRange : 0f;
    private float lastNoiseReceivedTime = float.NegativeInfinity;

    // Patrol
    public Vector3 PatrolOrigin { get; private set; }

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<EnemyHealth>();

        PatrolOrigin = transform.position;

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
        TickSuspicionDecay();
    }

    private void FixedUpdate()
    {
        StateMachine.CurrentState.PhysicsUpdate();
    }

    // Chase 중에는 감소 없음, 그 외 마지막 소음 감지 후 decay delay 경과 시 감소
    private void TickSuspicionDecay()
    {
        if (StateMachine.CurrentState == ChaseState) return;
        if (Time.time - lastNoiseReceivedTime < data.noiseSuspicionDecayDelay) return;
        NoiseSuspicionLevel = Mathf.Max(0f, NoiseSuspicionLevel - data.noiseSuspicionDecayRate * Time.deltaTime);
    }

    public void SetPatrolOrigin(Vector3 newOrigin)
    {
        PatrolOrigin = newOrigin;
    }

    public void OnNoiseDetected(Vector3 noisePosition, float noiseRadius)
    {
        DetectedNoisePosition = noisePosition;
        lastNoiseReceivedTime = Time.time;

        float distance = Vector3.Distance(transform.position, noisePosition);
        float ratio = Mathf.Clamp01(1f - distance / (noiseRadius + data.detectionRange));
        float gain = data.noiseSuspicionPerEvent * ratio;

        NoiseSuspicionLevel = Mathf.Clamp(NoiseSuspicionLevel + gain, 0f, 100f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        float t = NoiseSuspicionLevel / 100f;

        // 의심 수치 색상: 초록(안전) → 노랑(경계) → 빨강(추적)
        Color fillColor = t < 0.5f
            ? Color.Lerp(new Color(0.2f, 0.9f, 0.2f), new Color(1f, 0.9f, 0.1f), t * 2f)
            : Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(1f, 0.2f, 0.1f), (t - 0.5f) * 2f);

        Vector3 center = transform.position + Vector3.up * 0.05f;
        const float radius = 0.55f;

        // 배경 디스크 (회색)
        UnityEditor.Handles.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        UnityEditor.Handles.DrawSolidDisc(center, Vector3.up, radius);

        // 의심 수치 채움 부채꼴
        if (NoiseSuspicionLevel > 0f)
        {
            UnityEditor.Handles.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0.85f);
            UnityEditor.Handles.DrawSolidArc(center, Vector3.up, Vector3.forward, 360f * t, radius);
        }

        // 외곽선
        UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.6f);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);

        // 수치 레이블
        string stateLabel = NoiseSuspicionLevel >= 100f ? "추적" : NoiseSuspicionLevel >= 50f ? "경계" : "안전";
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.2f,
            $"의심 {NoiseSuspicionLevel:F0} / 100  [{stateLabel}]"
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        // 순찰 범위 (파란색)
        UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(PatrolOrigin, Vector3.up, data.patrolRadius);
        UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 1f);
        UnityEditor.Handles.DrawWireDisc(PatrolOrigin, Vector3.up, data.patrolRadius);

        // 소음 감지 범위 (노란색)
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.1f, 0.1f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, data.detectionRange);
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.1f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, data.detectionRange);

        // 공격 범위 (빨간색)
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.2f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, data.attackRange);
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, data.attackRange);
    }
#endif
}