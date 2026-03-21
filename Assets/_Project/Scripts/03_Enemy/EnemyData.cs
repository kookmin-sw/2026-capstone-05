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
    public float patrolRadius = 15f;
    public float searchSpeed = 3.5f;
    public float searchRadius = 6f;
    public float searchDuration = 5f;

    [Header("Detection Settings")]
    public float detectionRange = 10f;

    [Header("Combat Settings")]
    public float attackRange = 1.5f;
    public float attackDamage = 10f;
    public float attackCooldown = 1f;

}

