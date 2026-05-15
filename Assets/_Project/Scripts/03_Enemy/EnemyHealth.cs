using Fusion;
using System;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class EnemyHealth : NetworkBehaviour, IDamageable
{
    private EnemyAI enemy;
    private float localCurrentHealth;

    [Networked] private float NetworkCurrentHealth { get; set; }

    public event Action<EnemyHealth> Died;

    public float CurrentHealth => enemy != null && enemy.IsLocalSimulationActive
        ? localCurrentHealth
        : NetworkCurrentHealth;

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

    public void InitializeLocalHealth()
    {
        enemy = enemy != null ? enemy : GetComponent<EnemyAI>();
        if (enemy == null || enemy.Data == null)
            return;

        localCurrentHealth = enemy.Data.maxHealth;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (enemy != null && enemy.IsLocalSimulationActive)
        {
            TakeLocalDamage(info.damageAmount);
            return;
        }

        if (!HasStateAuthority || NetworkCurrentHealth <= 0f) return;

        float damage = Mathf.Min(info.damageAmount, NetworkCurrentHealth);
        NetworkCurrentHealth -= damage;

        if (NetworkCurrentHealth <= 0f)
        {
            NetworkCurrentHealth = 0f;
            Died?.Invoke(this);
            enemy.StateMachine.ChangeState(enemy.DeadState);
            return;
        }

        enemy.RegisterHitReaction();
    }

    private void TakeLocalDamage(float damageAmount)
    {
        if (localCurrentHealth <= 0f) return;

        float damage = Mathf.Min(damageAmount, localCurrentHealth);
        localCurrentHealth -= damage;

        if (localCurrentHealth <= 0f)
        {
            localCurrentHealth = 0f;
            Died?.Invoke(this);
            enemy.StateMachine.ChangeState(enemy.DeadState);
            return;
        }

        enemy.RegisterHitReaction();
    }
}
