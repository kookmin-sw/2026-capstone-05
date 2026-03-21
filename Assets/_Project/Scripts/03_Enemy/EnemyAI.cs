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

    // Noise Detection
    public Vector3 DetectedNoisePosition { get; private set; }
    public bool HasNoiseDetected { get; private set; }

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<EnemyHealth>();

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

    public void ListenNoise(Vector3 noisePosition)
    {
        DetectedNoisePosition = noisePosition;
        HasNoiseDetected = true;
    }

    public void ConsumeNoiseDetection()
    {
        HasNoiseDetected = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        // 순찰 반경 (파란색)
        UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, data.patrolRadius);
        UnityEditor.Handles.color = new Color(0.2f, 0.5f, 1f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, data.patrolRadius);

        // 플레이어 감지 범위 (노란색)
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