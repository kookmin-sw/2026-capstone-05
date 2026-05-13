using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "NUNBORA/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Enemy Name")]
    public string enemyName = "Enemy";

    [Header("Health Settings")]
    public float maxHealth = 100f;

    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float chaseSpeed = 4.5f;
    public float searchSpeed = 3f;
    public float rotationSpeed = 10f;

    [Header("Idle Settings")]
    public float idleStartTime = 1f;
    public float idleEndTime = 3f;

    [Header("Patrol Settings")]
    public float patrolRadius = 15f;
    [Range(0f, 1f)] public float patrolMinRadiusRatio = 0.2f;
    [Range(0f, 1f)] public float patrolMaxRadiusRatio = 1f;
    public float patrolMinAngleStep = 35f;
    public float patrolMaxAngleStep = 160f;
    [Range(0f, 1f)] public float patrolDirectionChangeChance = 0.2f;
    public int patrolDestinationAttempts = 8;
    
    [Header("Alert Settings")]
    public float alignAngleThreshold = 12f;
    public float alertArcRadiusMin = 0.2f;
    public float alertArcRadiusMax = 1.2f;

    [Header("Search Settings")]
    public float searchRadius = 6f;
    public float waitStartTime = 0.5f;
    public float waitEndTime = 1.5f;
    public float newNoisePositionThreshold = 0.25f;

    [Header("Suspicion Thresholds")]
    public float alertThreshold = 20f;
    public float lookThreshold = 40f;
    public float searchThreshold = 70f;
    public float searchExitThreshold = 30f;
    public float chaseThreshold = 100f;
    public float chaseExitThreshold = 80f;

    [Header("Suspicion Settings")]
    public float suspicionSensitivity = 0.5f;
    public float suspicionGainAmount = 100f;
    public float suspicionReduceRate = 15f;
    public float suspicionReduceDelay = 2f;

    [Header("Noise Memory")]
    [Range(1, 8)] public int noiseMemoryCapacity = 5;
    public float noiseMemoryDuration = 8f;
    public float noiseMemoryScoreDecayRate = 8f;
    public float noiseRetargetInterval = 0.5f;
    public float noiseRetargetMinStickTime = 1.5f;
    public float noiseRetargetScoreMargin = 10f;
    public float repeatedNoiseMergeRadius = 3f;
    public float repeatedNoiseBonus = 12f;
    public float obstructedNoiseScorePenalty = 8f;

    [Header("Noise Estimation")]
    public float maxNoisePositionError = 6f;
    public float obstructedNoisePositionErrorBonus = 4f;
    [Range(0.25f, 4f)] public float noisePositionErrorPower = 1.25f;
    public float navMeshSampleRange = 2f;

    [Header("Hit Reaction")]
    public float hitSuspicionGain = 55f;
    public float hitMinimumSuspicion = 40f;

    [Header("Combat Settings")]
    public float attackDamage = 10f;
    public float attackCooldown = 1f;
    public float attackRecoveryMinTime = 0.8f;
    public float attackRecoveryMaxTime = 1.25f;
    public float attackRadius = 2f;
    public float attackAngle = 60f;

    [Header("Animation Fail-safe")]
    public float attackAnimationFailSafeTime = 3f;
    public float hitAnimationFailSafeTime = 2f;
    public float deadAnimationFailSafeTime = 5f;
    public float turnAnimationFailSafeTime = 2f;
}

