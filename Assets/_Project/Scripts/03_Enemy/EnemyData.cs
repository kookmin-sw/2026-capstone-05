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
    public float chaseSpeed = 4f;
    public float searchSpeed = 3f;

    [Header("Patrol Settings")]
    public float patrolRadius = 15f;
    public float idleMinTime = 2f;
    public float idleMaxTime = 4f;

    [Header("Search Settings")]
    public float searchRadius = 6f;

    [Header("Detection Settings")]
    public float detectionRange = 10f;
 

    [Header("Noise Suspicion Settings")]
    [Tooltip("소음 진원지 감지 시 최대 증가량 (0~100). 거리에 비례해 감소.")]
    public float noiseSuspicionPerEvent = 100f;
    [Tooltip("의심 수치 초당 감소량")]
    public float noiseSuspicionDecayRate = 15f;
    [Tooltip("마지막 소음 감지 후 수치 감소까지 대기 시간 (초)")]
    public float noiseSuspicionDecayDelay = 2f;

    [Header("Combat Settings")]
    public float attackDamage = 10f;
    public float attackCooldown = 1f;
    public float attackRange = 1.5f;
}

