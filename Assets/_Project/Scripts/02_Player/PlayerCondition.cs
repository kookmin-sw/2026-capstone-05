using System;
using UnityEngine;

public class PlayerCondition : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public ConditionStat health;
    public ConditionStat stamina;
    public ConditionStat satiety;
    public ConditionStat coldness;

    public event Action<float> OnTakeDamageEvent;


    private void Start()
    {
        health.Initialize();
        stamina.Initialize();
        satiety.Initialize();
        coldness.Initialize();
    }

    private void Update()
    {
        health.UpdatePassive();
        stamina.UpdatePassive();
        satiety.UpdatePassive();
        coldness.UpdatePassive();
    }

    public bool IsAlive => health.currentValue > 0;

    public void TakeDamage(float damageAmount)
    {
        if (IsAlive)
        {
            health.Subtract(damageAmount);

            OnTakeDamageEvent?.Invoke(damageAmount);

            if (!IsAlive)
            {
                Debug.Log("Player has died.");
            }
        }
    }

    public bool UseStamina(float amount)
    {
        if (stamina.currentValue >= amount)
        {
            stamina.Subtract(amount);
            return true;
        }
        return false;
    }

}
