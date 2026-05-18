using FMODUnity;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform handSocket1P;
    [SerializeField] private Transform handSocket3P;

    [Header("IK Managers")]
    [SerializeField] private PlayerIKManager ikManager1P;
    [SerializeField] private PlayerIKManager ikManager3P;

    public EquippedItemBehaviour Item1P { get; private set; }
    public EquippedItemBehaviour Item3P { get; private set; }
    public ItemInstance CurrentItemInstance { get; private set; }

    public event System.Action<ItemInstance> OnEquippedItemChanged;
    public event System.Action<ItemUseAnimationType> OnUseAnimationRequested;

    private GameObject currentObj1P;
    private GameObject currentObj3P;

    private Transform cameraTransform;

    [Header("Unarmed Settings")]
    public float unarmedDamage = 5f;
    public float unarmedAttackCooldown = 0.8f;
    public float unarmedRange = 1f;
    public float unarmedHitRadius = 0.3f;
    public LayerMask hitLayerMask = ~0;

    public EventReference unarmedSwingEvent;
    public EventReference unarmedHitEvent;

    private float lastUnarmedAttackTime = -999f;

    private void Start()
    {
        cameraTransform = player.CameraTransform;
    }

    /// <summary>
    /// 인벤토리(퀵슬롯)에서 아이템을 장착할 때 호출됩니다.
    /// </summary>
    public void EquipItem(ItemInstance itemInstance)
    {
        EquipItem(itemInstance, true);
    }

    public void EquipItem(ItemInstance itemInstance, bool notifyNetwork)
    {
        CancelCurrentItemUse();
        UnequipItem(false);

        if (itemInstance == null || itemInstance.Data == null)
        {
            CurrentItemInstance = null;
            if (notifyNetwork)
                OnEquippedItemChanged?.Invoke(null);
            return;
        }

        CurrentItemInstance = itemInstance;
        ItemDataRegistry.Register(itemInstance.Data);

        GameObject prefab = itemInstance.Data.equipPrefab;
        if (prefab == null)
        {
            if (notifyNetwork)
                OnEquippedItemChanged?.Invoke(itemInstance);
            return;
        }

        player.Animator.SetItemPose(itemInstance.Data.poseType);
        Debug.Log($"Equipping item: {itemInstance.Data.itemName} with pose {itemInstance.Data.poseType}");

        if (player.IsLocalPlayer && handSocket1P != null)
        {
            currentObj1P = Instantiate(prefab, handSocket1P);
            Item1P = currentObj1P.GetComponent<EquippedItemBehaviour>();

            if (Item1P != null)
            {
                Item1P.Initialize(player, itemInstance, true);
            }
        }

        if (handSocket3P != null)
        {
            currentObj3P = Instantiate(prefab, handSocket3P);
            Item3P = currentObj3P.GetComponent<EquippedItemBehaviour>();

            if (Item3P != null)
            {
                Item3P.Initialize(player, itemInstance, false);
            }
        }

        if (player.IsLocalPlayer && Item1P != null && ikManager1P != null)
        {
            ikManager1P.SetLeftHandWeaponGrip(Item1P.leftHandGrip);
        }

        if (Item3P != null && ikManager3P != null)
        {
            ikManager3P.SetLeftHandWeaponGrip(Item3P.leftHandGrip);
        }

        if (notifyNetwork)
            OnEquippedItemChanged?.Invoke(itemInstance);
    }

    /// <summary>
    /// 무기를 집어넣거나 다른 무기로 스왑할 때 호출됩니다.
    /// </summary>
    public void UnequipItem()
    {
        UnequipItem(true);
    }

    public void UnequipItem(bool notifyNetwork)
    {
        CancelCurrentItemUse();

        if (currentObj1P != null)
        {
            Destroy(currentObj1P);
        }
        else if (Item1P != null)
        {
            Destroy(Item1P.gameObject);
        }

        if (currentObj3P != null)
        {
            Destroy(currentObj3P);
        }
        else if (Item3P != null)
        {
            Destroy(Item3P.gameObject);
        }

        player.Animator.SetItemPose(ItemPoseType.Default);

        currentObj1P = null;
        currentObj3P = null;
        Item1P = null;
        Item3P = null;
        CurrentItemInstance = null;

        if (ikManager1P != null)
        {
            ikManager1P.SetLeftHandWeaponGrip(null);
        }
        if (ikManager3P != null)
        {
            ikManager3P.SetLeftHandWeaponGrip(null);
        }

        if (notifyNetwork)
            OnEquippedItemChanged?.Invoke(null);
    }

    /// <summary>
    /// InputHandler에서 마우스 좌클릭 시 호출합니다.
    /// </summary>
    public void UseCurrentItem()
    {
        if (!player.IsLocalPlayer)
        {
            return;
        }

        if (Item1P != null)
        {
            if (Item1P.Use())
                OnUseAnimationRequested?.Invoke(CurrentItemInstance?.Data?.useAnimationType ?? ItemUseAnimationType.None);
        }
        else
        {
            if (Time.time - lastUnarmedAttackTime >= unarmedAttackCooldown)
            {
                lastUnarmedAttackTime = Time.time;
                player.Animator.PlayUseItemAnimation(ItemUseAnimationType.UnarmedAttack);
                OnUseAnimationRequested?.Invoke(ItemUseAnimationType.UnarmedAttack);
            }
        }
    }

    public void CancelCurrentItemUse()
    {
        // 현재 손에 들고 있는 아이템 스크립트가 있다면 취소 명령 전달
        if (Item1P != null)
        {
            Item1P.CancelUse();
        }
        if (Item3P != null)
        {
            Item3P.CancelUse();
        }
    }

    /// <summary>
    /// ⭐️ PlayerAnimator(문지기)가 1P/3P 중복을 걸러내고 순수하게 넘겨준 단일 이벤트입니다.
    /// </summary>
    public void HandleAnimationEvent()
    {
        if (Item1P != null)
        {
            Item1P.OnAnimationEventTriggered();
        }
        else
        {
            PerformUnarmedHitCheck();
        }
    }

    /// <summary>
    /// 맨손(주먹) 타격 판정 및 데미지 적용
    /// </summary>
    private void PerformUnarmedHitCheck()
    {
        Transform originTransform = player.CameraTransform != null ? player.CameraTransform : Camera.main.transform;
        Vector3 checkCenter = originTransform.position + originTransform.forward * unarmedRange;

        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, unarmedHitRadius, hitLayerMask);
        if (hitColliders.Length > 0)
        {
            foreach (var hit in hitColliders)
            {
                if (hit.TryGetComponent(out IDamageable target))
                {
                    DamageInfo damageInfo = new DamageInfo
                    {
                        damageAmount = unarmedDamage,
                        hitPoint = hit.ClosestPoint(originTransform.position),
                        hitNormal = (hit.transform.position - originTransform.position).normalized,
                        attacker = player.gameObject
                    };
                    target.TakeDamage(damageInfo);
                }
            }
            RuntimeManager.PlayOneShot(unarmedHitEvent, checkCenter);
        }
        else
        {
            RuntimeManager.PlayOneShot(unarmedSwingEvent, checkCenter);
        }
    }
}
