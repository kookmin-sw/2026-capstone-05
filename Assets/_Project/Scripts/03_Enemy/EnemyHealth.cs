using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    private EnemyAI enemy;
    private EnemyData enemyData;
    private float currentHealth;

    private void Awake()
    {
        enemy = GetComponent<EnemyAI>();
        if (enemy != null)
            enemyData = enemy.Data;
    }

    private void Start()
    {
        if (enemyData != null)
            currentHealth = enemyData.maxHealth;
    }

    public void TakeDamage(float damageAmount)
    {
        float damage = Mathf.Min(damageAmount, currentHealth);
        currentHealth -= damage;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            enemy.StateMachine.ChangeState(enemy.DeadState);
            return;
        }

        if (enemy.StateMachine.CurrentState != enemy.DeadState &&
            enemy.StateMachine.CurrentState != enemy.AttackState)
        {
            enemy.StateMachine.ChangeState(enemy.HitState);
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        
    }
}
