public enum ConditionType
{
    Health,
    Stamina,
    Satiety,
    Coldness
}

[System.Serializable]
public class StatusEffectData
{
    public ConditionType targetStat;
    public float totalAmount;
    public float duration; // 0이면 즉시 적용
    public float AmountPerSecond => duration > 0f ? (totalAmount / duration) : totalAmount;
}

public class ActiveEffect
{
    public StatusEffectData Data { get; private set; }
    public float TimeRemaining { get; private set; }

    public ActiveEffect(StatusEffectData data)
    {
        Data = data;
        TimeRemaining = data.duration;
    }

    public bool Tick(float deltaTime, PlayerCondition condition)
    {
        float frameAmount = Data.AmountPerSecond * deltaTime;
        ApplyToStat(frameAmount, condition);

        TimeRemaining -= deltaTime;

        return TimeRemaining <= 0f;
    }

    private void ApplyToStat(float amount, PlayerCondition condition)
    {
        ConditionStat targetStat = Data.targetStat switch
        {
            ConditionType.Health => condition.health,
            ConditionType.Stamina => condition.stamina,
            ConditionType.Satiety => condition.satiety,
            ConditionType.Coldness => condition.coldness,
            _ => null
        };

        if (targetStat != null)
        {
            targetStat.Add(amount);
        }
    }
}