using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform handSocket1P;

    public EquippedItemBehaviour CurrentItem { get; private set; }

    [Header("Unarmed Settings")]
    public float unarmedDamage = 5f;
    public float unarmedRange = 1f;
    public float unarmedAttackCooldown = 0.8f;

    private float lastUnarmedAttackTime = 0f;

    /// <summary>
    /// 인벤토리(퀵슬롯)에서 아이템을 선택했을 때 호출됩니다.
    /// </summary>
    public void EquipItem(ItemInstance itemInstance)
    {
        if (CurrentItem != null)
        {
            Destroy(CurrentItem.gameObject);
        }

        if (itemInstance.Data.equipPrefab == null)
        {
            Debug.Log("이 아이템은 손에 들 수 없습니다.");
            return;
        }

        GameObject spawnedItem = Instantiate(itemInstance.Data.equipPrefab, handSocket1P);

        CurrentItem = spawnedItem.GetComponent<EquippedItemBehaviour>();
        if (CurrentItem != null)
        {
            CurrentItem.Initialize(player, itemInstance);
        }
    }

    /// <summary>
    /// PlayerInputHandler에서 좌클릭(Action)을 감지했을 때 호출됩니다.
    /// </summary>
    public void UseCurrentItem()
    {
        if (CurrentItem != null)
        {
            CurrentItem.Use();
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
    /// PlayerAnimator에서 애니메이션 이벤트가 발생했을 때 호출됩니다.
    /// </summary>
    public void HandleAnimationEvent()
    {
        if (CurrentItem != null)
        {
            CurrentItem.OnAnimationEventTriggered();
        }
        else
        {
            PerformUnarmedHitCheck();
        }
    }

    private void PerformUnarmedHitCheck()
    {
        // 여기에 맨손 전용 Raycast나 BoxCast 로직 작성
        // 예: Physics.SphereCast(player.CameraRoot.position, 0.5f, player.CameraRoot.forward, out hit, unarmedRange)
    }
}