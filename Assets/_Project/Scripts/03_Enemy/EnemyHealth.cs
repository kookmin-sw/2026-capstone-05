using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    private EnemyAI enemy;
    private float currentHealth;

    private void Awake()
    {
        enemy = GetComponent<EnemyAI>();
    }

    private void Start()
    {
        currentHealth = enemy.Data.maxHealth;
    }

    public void TakeDamage(float damageAmount)
    {
        if (currentHealth <= 0f) return;

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
}
