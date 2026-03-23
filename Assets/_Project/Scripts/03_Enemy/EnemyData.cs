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
    public float chaseSpeed = 5f;
    public float searchSpeed = 4f;
    public float rotationSpeed = 30f;

    [Header("Patrol Settings")]
    public float patrolRadius = 15f;

    [Header("Search Settings")]
    public float searchRadius = 6f;

    [Header("Detection Settings")]
    public float detectionRadius = 10f;

    [Header("Suspicion Settings")]
    public float suspicionGainAmount = 100f;
    public float suspicionReduceRate = 15f;
    public float suspicionReduceDelay = 2f;

    [Header("Combat Settings")]
    public float attackDamage = 10f;
    public float attackCooldown = 1f;
    public float attackRadius = 2f;
}

