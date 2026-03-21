using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    private EnemyAI enemy;
    private EnemyData enemyData;

    private void Awake()
    {
        enemy = GetComponent<EnemyAI>();
        if (enemy != null)
            enemyData = enemy.Data;
    }

    private void Start()
    {

    }

    public void TakeDamage(float damageAmount) 
    { 
        
    }
}
