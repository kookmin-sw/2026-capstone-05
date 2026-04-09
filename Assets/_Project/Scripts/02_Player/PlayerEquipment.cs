using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform handSocket1P;
    [SerializeField] private Transform handSocket3P;

    public EquippedItemBehaviour Item1P { get; private set; }
    public EquippedItemBehaviour Item3P { get; private set; }

    private GameObject currentObj1P;
    private GameObject currentObj3P;

    private Transform cameraTransform;

    [Header("Unarmed Settings")]
    public float unarmedDamage = 5f;
    public float unarmedAttackCooldown = 0.8f;
    public float unarmedRange = 1f;
    public float unarmedHitRadius = 0.3f;
    public LayerMask hitLayerMask = ~0;

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
        UnequipItem();

        if (itemInstance == null || itemInstance.Data == null)
        {
            return;
        }

        GameObject prefab = itemInstance.Data.equipPrefab;
        if (prefab == null)
        {
            return;
        }

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

        //if (player.IsLocalPlayer && Item1P != null)
        //{
        //    player.IKManager.SetWeaponGrips(Item1P.leftHandGrip, Item1P.rightHandGrip);
        //}
        //else if (!player.IsLocalPlayer && Item3P != null)
        //{
        //    player.IKManager.SetWeaponGrips(Item3P.leftHandGrip, Item3P.rightHandGrip);
        //}
    }

    /// <summary>
    /// 무기를 집어넣거나 다른 무기로 스왑할 때 호출됩니다.
    /// </summary>
    public void UnequipItem()
    {
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

        currentObj1P = null;
        currentObj3P = null;
        Item1P = null;
        Item3P = null;

        //player.IKManager.SetWeaponGrips(null, null);
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
            Item1P.Use();
        }
        else
        {
            if (Time.time - lastUnarmedAttackTime >= unarmedAttackCooldown)
            {
                lastUnarmedAttackTime = Time.time;
                player.Animator.PlayUseItemAnimation(ItemUseAnimationType.None);
            }
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
        Transform originTransform = cameraTransform != null ? cameraTransform : Camera.main.transform;

        Ray ray = new Ray(originTransform.position, originTransform.forward);
        if (Physics.SphereCast(ray, unarmedHitRadius, out RaycastHit hit, unarmedRange, hitLayerMask))
        {
            if (hit.collider.TryGetComponent(out IDamageable target))
            {
                target.TakeDamage(unarmedDamage);
                // TODO: 적중 타격음 발생
            }
            else
            {
                // TODO: 벽/사물 타격음 발생
            }
        }
        else
        {
            // TODO: 허공 헛스윙 소리 발생
        }
    }
}