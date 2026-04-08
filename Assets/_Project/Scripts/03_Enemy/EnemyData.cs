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
    
    [Header("Alert Settings")]
    public float alignAngleThreshold = 12f;
    public float alertArcRadiusMin = 0.2f;
    public float alertArcRadiusMax = 1.2f;

    [Header("Search Settings")]
    public float searchRadius = 6f;
    public float waitStartTime = 0.5f;
    public float waitEndTime = 1.5f;

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

    [Header("Combat Settings")]
    public float attackDamage = 10f;
    public float attackCooldown = 1f;
    public float attackRadius = 2f;
    public float attackAngle = 60f;
}

