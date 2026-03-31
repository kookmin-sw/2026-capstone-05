using UnityEngine;

/// <summary>
/// 플레이어 손에 장착되는 모든 아이템 프리팹의 최상위 부모 클래스
/// </summary>
public abstract class EquippedItemBehaviour : MonoBehaviour
{
    protected PlayerController player;
    protected ItemInstance itemInstance;

    public virtual void Initialize(PlayerController owner, ItemInstance instance)
    {
        player = owner;
        itemInstance = instance;
    }

    /// <summary>
    /// 좌클릭(Primary Use) 시 호출되는 메서드.
    /// </summary>
    public virtual void Use()
    {
        player.Animator.PlayUseItemAnimation(itemInstance.Data.useAnimationType);
    }

    /// <summary>
    /// 우클릭(Secondary Use) 시 호출되는 메서드. (예: 조준, 특수 능력 등)
    /// </summary>
    public virtual void SecondaryUse() { }
}