using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class EnemyHealth : NetworkBehaviour, IDamageable
{
    private EnemyAI enemy;

    [Networked] private float NetworkCurrentHealth { get; set; }

    public float CurrentHealth => NetworkCurrentHealth;

    private void Awake()
    {
        enemy = GetComponent<EnemyAI>();
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetworkCurrentHealth = enemy.Data.maxHealth;
        }
    }

    public void TakeDamage(float damageAmount)
    {
        if (!HasStateAuthority || NetworkCurrentHealth <= 0f) return;

        float damage = Mathf.Min(damageAmount, NetworkCurrentHealth);
        NetworkCurrentHealth -= damage;

        if (NetworkCurrentHealth <= 0f)
        {
            NetworkCurrentHealth = 0f;
            enemy.StateMachine.ChangeState(enemy.DeadState);
            return;
        }

        if (enemy.StateMachine.CurrentState != enemy.DeadState &&
        enemy.StateMachine.CurrentState != enemy.AttackState &&
        enemy.StateMachine.CurrentState != enemy.HitState)
        {
            enemy.StateMachine.ChangeState(enemy.HitState);
        }
    }
}
