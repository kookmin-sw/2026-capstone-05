using System;
using UnityEngine;

[System.Serializable]
public class ConditionStat
{
    public float maxValue = 100f;
    public float startValue = 100f;
    public float increaseRate = 0f; // 초당 상승량
    public float decreaseRate = 0f; // 초당 감소량

    [HideInInspector] public float currentValue;

    public event Action<float, float> OnValueChanged; // 값이 변경될 때 발생하는 이벤트 <현재 값, 최대 값>

    public void Initialize()
    {
        currentValue = startValue;
        OnValueChanged?.Invoke(currentValue, maxValue);
    }

    public void Add(float amount)
    {
        currentValue = Mathf.Clamp(currentValue + amount, 0, maxValue);
        OnValueChanged?.Invoke(currentValue, maxValue);
    }

    public void Subtract(float amount)
    {
        currentValue = Mathf.Clamp(currentValue - amount, 0, maxValue);
        OnValueChanged?.Invoke(currentValue, maxValue);
    }

    public void SetValue(float value)
{
    SetCurrentValue(value);
}

public void SetCurrentValue(float value)
{
    float clampedValue = Mathf.Clamp(value, 0, maxValue);
    if (Mathf.Approximately(currentValue, clampedValue))
    {
        return;
    }

    currentValue = clampedValue;
    OnValueChanged?.Invoke(currentValue, maxValue);
}

    /// <summary>
    /// 초당 회복량 및 감소량을 적용하여 현재 값을 갱신하는 메서드
    /// * 이 메서드는 매 프레임마다 호출되어야 합니다. (e.g. Update())
    /// </summary>
    public void UpdatePassive()
    {
        if (increaseRate > 0f && currentValue < maxValue)
        {
            Add(increaseRate * Time.deltaTime);
        }

        if (decreaseRate > 0f && currentValue > 0f)
        {
            Subtract(decreaseRate * Time.deltaTime);
        }
    }
}
