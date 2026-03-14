using UnityEngine;

public class PlayerCondition : MonoBehaviour
{
    public float maxHealth = 100f;
    public float CurrentHealth { get; private set; }

    public float maxStamina = 100f;
    public float CurrentStamina { get; private set; }

    /// <summary>
    /// 배고픔 수치 (Satiety Level): 0(굶주림) - 100(포만감)
    /// </summary>
    public float CurrentSatietyLevel { get; private set; } = 100f;
    /// <summary>
    /// 체온 수치 (Warmth Level): 0(얼음) - 100(따뜻함)
    /// </summary>
    public float CurrentWarmthLevel { get; private set; } = 100f;


    private void Start()
    {
        CurrentHealth = maxHealth;
        CurrentStamina = maxStamina;
    }


    public bool IsAlive => CurrentHealth > 0;
    public void TakeDamage(float amount)
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, maxHealth);
    }
    public void Heal(float amount)
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, maxHealth);
    }

    public bool CanConsumeStamina(float amount)
    {
        return CurrentStamina >= amount;
    }
    public void ConsumeStamina(float amount)
    {
        CurrentStamina = Mathf.Clamp(CurrentStamina - amount, 0, maxStamina);
    }
    public void RecoverStamina(float amount)
    {
        CurrentStamina = Mathf.Clamp(CurrentStamina + amount, 0, maxStamina);
    }

    public void DecreaseSatiety(float amount)
    {
        CurrentSatietyLevel = Mathf.Clamp(CurrentSatietyLevel - amount, 0, 100);
    }
    public void IncreaseSatiety(float amount)
    {
        CurrentSatietyLevel = Mathf.Clamp(CurrentSatietyLevel + amount, 0, 100);
    }

    public void DecreaseWarmth(float amount)
    {
        CurrentWarmthLevel = Mathf.Clamp(CurrentWarmthLevel - amount, 0, 100);
    }
    public void IncreaseWarmth(float amount)
    {
        CurrentWarmthLevel = Mathf.Clamp(CurrentWarmthLevel + amount, 0, 100);
    }
}
