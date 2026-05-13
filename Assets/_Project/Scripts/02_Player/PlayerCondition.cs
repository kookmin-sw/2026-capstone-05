using FMODUnity;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCondition : MonoBehaviour, IDamageable, IPlayerNetworkConfigurable
{
    [Header("Stats")]
    public ConditionStat health;
    public ConditionStat stamina;
    public ConditionStat satiety;
    public ConditionStat coldness;

    private List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    [Header("Stamina System")]
    private float staminaRegenTimer = 0f;

    public event Action<float> OnTakeDamageEvent;

    public event Action OnDiedEvent;

    private bool hasRaisedDeathEvent;

    [Header("Sound Settings")]
    [SerializeField] private EventReference damageSound;

    private PlayerController controller;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

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
        //stamina.UpdatePassive();
        satiety.UpdatePassive();
        coldness.UpdatePassive();

        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
        }
        else
        {
            stamina.UpdatePassive();
        }

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            bool isFinished = activeEffects[i].Tick(Time.deltaTime, this);

            if (isFinished)
            {
                activeEffects.RemoveAt(i);
            }
        }
    }

    public bool IsAlive => health.currentValue > 0;

    public void TakeDamage(float damageAmount)
    {
        if (IsAlive)
        {
            health.Subtract(damageAmount);

            OnTakeDamageEvent?.Invoke(damageAmount);

            RuntimeManager.PlayOneShot(damageSound, transform.position);

            if (!IsAlive)
            {
                if (!hasRaisedDeathEvent)
                {
                    hasRaisedDeathEvent = true;
                    OnDiedEvent?.Invoke();
                }
                Debug.Log("Player has died.");
            }
        }
    }

    public void ReviveToFull()
    {
        health.Add(health.maxValue);
        stamina.Add(stamina.maxValue);
        satiety.Add(satiety.maxValue);
        coldness.Add(coldness.maxValue);

        hasRaisedDeathEvent = false;
    }

    /// <summary>
    /// 점프, 공격 등 단발성으로 스태미나를 깎을 때 사용
    /// </summary>
    public bool UseStamina(float amount)
    {
        if (stamina.currentValue >= amount)
        {
            stamina.Subtract(amount);
            if (controller != null)
            {
                staminaRegenTimer = controller.staminaRegenDelay;
            }

            return true;
        }
        return false;
    }

    /// <summary>
    /// 달리기 등 매 프레임 지속적으로 스태미나를 깎을 때 사용
    /// </summary>
    public void DrainStamina(float amount)
    {
        stamina.Subtract(amount);

        if (controller != null)
        {
            staminaRegenTimer = controller.staminaRegenDelay;
        }
    }

    /// <summary>
    /// 외부(아이템, 함정 등)에서 플레이어에게 버프/디버프를 걸 때 호출
    /// </summary>
    public void ApplyEffect(StatusEffectData effectData)
    {
        if (effectData.duration <= 0f)
        {
            // 즉시 회복 아이템인 경우 (리스트에 넣지 않고 한 번만 틱 돌리고 끝)
            ActiveEffect instantEffect = new ActiveEffect(effectData);
            instantEffect.Tick(1f, this);
        }
        else
        {
            // 지속 아이템인 경우 관리 리스트에 등록
            activeEffects.Add(new ActiveEffect(effectData));

            // TODO: 나중에 UI 매니저에게 "새 버프 아이콘 띄워라" 라고 이벤트 쏠 수 있음
        }
    }

    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        BackendPlayerNetworkSync networkSync = GetComponent<BackendPlayerNetworkSync>();
        enabled = isLocalPlayer || networkSync == null || networkSync.HasStateAuthority;
    }

}
