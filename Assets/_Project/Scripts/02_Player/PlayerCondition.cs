using FMODUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.VFX;

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

    public event Action OnReviveEvent;
    public event Action OnDiedEvent;

    private bool hasRaisedDeathEvent;
    private bool statsInitialized;

    [Header("Condition Damage Settings")]
    public float conditionDamageInterval = 1f; // 상태로 인한 피해 간격 (초)
    public float hungerDamageRate = 0.5f; // 배고픔으로 인한 피해량
    public float coldDamageRate = 0.5f; // 추위로 인한 피해량

    private float conditionDamageTimer = 0f;

    [Header("Indoor Settings")]
    private bool isIndoors;
    public float indoorColdnessDecreaseRate = 0.4f;
    public float outdoorColdnessIncreaseRate = 0.2f;

    [Header("VFX Settings")]
    [SerializeField] private GameObject damageVFXPrefab;

    private ObjectPool<GameObject> damageVFXPool;

    [Header("Sound Settings")]
    [SerializeField] private EventReference damageSound;
    [SerializeField] private EventReference deathSound;

    private PlayerController controller;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();

        damageVFXPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(damageVFXPrefab, transform),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: (obj) => Destroy(obj),
            collectionCheck: false,
            defaultCapacity: 10,
            maxSize: 20
        );
    }

    private void Start()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        if (statsInitialized)
        {
            return;
        }

        health.Initialize();
        stamina.Initialize();
        satiety.Initialize();
        coldness.Initialize();
        statsInitialized = true;

        Collider[] overlappingColliders = Physics.OverlapSphere(transform.position, 0.1f);
        foreach (var collider in overlappingColliders)
        {
            if (collider.CompareTag("IndoorsTrigger"))
            {
                SetIndoors(true);
                break;
            }
        }
    }

    private void Update()
    {
        EnsureInitialized();

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

        if (IsAlive)
        {
            if (conditionDamageTimer > 0f)
            {
                conditionDamageTimer -= Time.deltaTime;
            }
            else
            {
                if (satiety.currentValue <= 0f)
                {
                    health.Subtract(hungerDamageRate);
                }
                if (coldness.currentValue >= coldness.maxValue)
                {
                    health.Subtract(coldDamageRate);
                }

                conditionDamageTimer = conditionDamageInterval;
            }
            
        }
    }

    public bool IsAlive => health.currentValue > 0;

    public void TakeDamage(DamageInfo info)
    {
        if (IsAlive)
        {
            health.Subtract(info.damageAmount);
            OnTakeDamageEvent?.Invoke(info.damageAmount);

            if (damageVFXPrefab != null)
            {
                GameObject vfx = damageVFXPool.Get();
                vfx.transform.position = info.hitPoint;
                vfx.transform.rotation = Quaternion.LookRotation(info.hitNormal);
                StartCoroutine(ReturnVFXToPoolAfterDelay(vfx, vfx.GetComponent<ParticleSystem>().main.duration));
            }

            NoiseManager.Instance.GenerateNoise(transform.position, NoiseData.NoiseType.Pain);

            if (!IsAlive)
            {
                if (!hasRaisedDeathEvent)
                {
                    hasRaisedDeathEvent = true;
                    OnDiedEvent?.Invoke();
                }
                Debug.Log("Player has died.");

                RuntimeManager.PlayOneShot(deathSound, transform.position);
            }
            else
            {
                RuntimeManager.PlayOneShot(damageSound, transform.position);
            }
        }
    }

    public void Revive()
    {
        OnReviveEvent?.Invoke();

        health.Add(health.maxValue);
        stamina.Add(stamina.maxValue);
        satiety.Add(satiety.maxValue);
        coldness.Subtract(coldness.maxValue);

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

    public void SetIndoors(bool indoors)
    {
        isIndoors = indoors;

        if (isIndoors)
        {
            coldness.increaseRate = 0f;
            coldness.decreaseRate = indoorColdnessDecreaseRate;
        }
        else
        {
            coldness.increaseRate = outdoorColdnessIncreaseRate;
            coldness.decreaseRate = 0f;
        }
    }

    private IEnumerator ReturnVFXToPoolAfterDelay(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        damageVFXPool.Release(vfx);
    }

}
